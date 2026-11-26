// @vitest-environment node
import { randomBytes } from "node:crypto";
import { NextRequest } from "next/server";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { POST as login } from "../app/api/auth/login/route";
import { POST as logout } from "../app/api/auth/logout/route";
import { POST as mfa } from "../app/api/auth/mfa/route";
import { proxy } from "../proxy";
import { type Session, SESSION_COOKIE, sealSession, unsealSession } from "./session";

const ORIGIN = "https://admin.minibox.test";

function tokens(suffix: string, accessTtlMs = 300_000): Session {
  return {
    accessToken: `access-${suffix}`,
    accessTokenExpiresAt: new Date(Date.now() + accessTtlMs).toISOString(),
    refreshToken: `refresh-${suffix}`,
    refreshTokenExpiresAt: new Date(Date.now() + 1_800_000).toISOString(),
    authMethods: ["pwd"],
  };
}

function request(path: string, init: { body?: unknown; origin?: string | null; session?: Session; method?: string } = {}) {
  const headers = new Headers({ "content-type": "application/json" });
  if (init.origin !== null) headers.set("origin", init.origin ?? ORIGIN);
  if (init.session) headers.set("cookie", `${SESSION_COOKIE}=${sealSession(init.session)}`);
  return new NextRequest(ORIGIN + path, {
    method: init.method ?? "POST",
    headers,
    body: init.body === undefined ? undefined : JSON.stringify(init.body),
  });
}

function cookieValue(setCookie: string) {
  return setCookie.match(new RegExp(`${SESSION_COOKIE}=([^;]*)`))?.[1];
}

const fetchMock = vi.fn<typeof fetch>();
// openapi-fetch gọi fetch(Request).
const call = (i: number) => (fetchMock.mock.calls[i]?.[0] as Request | undefined) ?? expect.unreachable(`identity chưa được gọi lần ${i + 1}`);

beforeEach(() => {
  vi.stubEnv("ADMIN_SESSION_KEY", randomBytes(32).toString("base64"));
  vi.stubEnv("IDENTITY_URL", "http://identity:8080");
  vi.stubEnv("ADMIN_ORIGIN", ORIGIN);
  vi.stubGlobal("fetch", fetchMock);
});

afterEach(() => {
  vi.unstubAllEnvs();
  vi.unstubAllGlobals();
  fetchMock.mockReset();
});

test("ac208_1 ac212_4 đăng nhập + MFA tạo cookie httpOnly; Secure; SameSite=Lax, trình duyệt không nhận token", async () => {
  const pwd = tokens("pwd");
  fetchMock.mockResolvedValueOnce(Response.json(pwd));
  const loggedIn = await login(request("/api/auth/login", { body: { identifier: "an@minibox.vn", password: "x" } }));
  expect(loggedIn.status).toBe(204);

  const both = { ...tokens("mfa"), authMethods: ["pwd", "mfa"] };
  fetchMock.mockResolvedValueOnce(Response.json(both));
  const res = await mfa(request("/api/auth/mfa", { body: { code: "123456" }, session: pwd }));
  expect(res.status).toBe(204);

  const verify = call(1);
  expect(verify.url).toBe("http://identity:8080/auth/mfa/verify");
  expect(verify.headers.get("authorization")).toBe("Bearer access-pwd");
  expect(verify.headers.get("idempotency-key")).toBeTruthy();
  expect(await verify.json()).toEqual({ code: "123456", refreshToken: "refresh-pwd" });

  const setCookie = res.headers.get("set-cookie") ?? "";
  expect(setCookie).toMatch(/HttpOnly/i);
  expect(setCookie).toMatch(/Secure/i);
  expect(setCookie).toMatch(/SameSite=Lax/i);
  expect(setCookie).toMatch(/Path=\//);
  const browserSees = setCookie + (await res.text());
  expect(browserSees).not.toContain("access-");
  expect(browserSees).not.toContain("refresh-");
  expect(unsealSession(cookieValue(setCookie))).toEqual(both);
});

test("Route Handler từ chối request ghi sai hoặc thiếu Origin (CSRF), không gọi identity", async () => {
  for (const origin of ["https://evil.test", null]) {
    const res = await login(request("/api/auth/login", { body: {}, origin }));
    expect(res.status).toBe(403);
  }
  expect((await logout(request("/api/auth/logout", { origin: "https://evil.test", session: tokens("a") }))).status).toBe(403);
  expect(fetchMock).not.toHaveBeenCalled();
});

test("Lỗi identity trả { code } cùng mã HTTP; MFA khi chưa có phiên → 401", async () => {
  fetchMock.mockResolvedValueOnce(Response.json({ code: "Identity.InvalidCredentials", detail: "x" }, { status: 400 }));
  const res = await login(request("/api/auth/login", { body: { identifier: "a", password: "b" } }));
  expect(res.status).toBe(400);
  expect(await res.json()).toEqual({ code: "Identity.InvalidCredentials" });
  expect((await mfa(request("/api/auth/mfa", { body: { code: "1" } }))).status).toBe(401);
});

test("Cookie bị sửa hoặc sai khóa coi như chưa đăng nhập", () => {
  const sealed = sealSession(tokens("a"));
  const tampered = Buffer.from(sealed, "base64url");
  tampered[30] = (tampered[30] ?? 0) ^ 1;
  expect(unsealSession(tampered.toString("base64url"))).toBeUndefined();
  vi.stubEnv("ADMIN_SESSION_KEY", randomBytes(32).toString("base64"));
  expect(unsealSession(sealed)).toBeUndefined();
});

test("Proxy làm mới access sắp hết hạn một lần cho các request đồng thời và ghi cookie mới", async () => {
  const old = tokens("old", 10_000);
  const fresh = tokens("new");
  fetchMock.mockResolvedValueOnce(Response.json(fresh));

  const responses = await Promise.all([1, 2, 3].map(() => proxy(request("/", { method: "GET", session: old }))));
  expect(fetchMock).toHaveBeenCalledTimes(1);
  expect(await call(0).json()).toEqual({ refreshToken: "refresh-old" });
  for (const res of responses) {
    expect(unsealSession(cookieValue(res.headers.get("set-cookie") ?? ""))).toEqual(fresh);
    // Request hiện tại cũng thấy cookie mới (Next chuyển header qua x-middleware-request-cookie).
    expect(res.headers.get("x-middleware-request-cookie") ?? "").toContain(SESSION_COOKIE);
  }

  expect((await proxy(request("/", { method: "GET", session: fresh }))).headers.get("set-cookie")).toBeNull();
});

test("Proxy xóa cookie khi phiên hết hạn hoặc bị thu hồi", async () => {
  fetchMock.mockResolvedValueOnce(Response.json({ code: "Identity.InvalidRefreshToken" }, { status: 401 }));
  const res = await proxy(request("/", { method: "GET", session: tokens("revoked", 0) }));
  expect(res.headers.get("set-cookie")).toMatch(/Max-Age=0/i);
});

test("Đăng xuất thu hồi phiên ở identity và xóa cookie", async () => {
  fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }));
  const res = await logout(request("/api/auth/logout", { session: tokens("a") }));
  expect(res.status).toBe(204);
  expect(call(0).url).toBe("http://identity:8080/auth/logout");
  expect(await call(0).json()).toEqual({ refreshToken: "refresh-a" });
  expect(res.headers.get("set-cookie")).toMatch(/Max-Age=0/i);
});
