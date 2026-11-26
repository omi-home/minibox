import { afterEach, expect, test, vi } from "vitest";
import { createIdentityClient } from "./index";

afterEach(() => vi.unstubAllGlobals());

test("Request ghi tự kèm Idempotency-Key, GET thì không; giữ key do caller đặt", async () => {
  const fetchMock = vi.fn<typeof fetch>(async () => new Response(null, { status: 204 }));
  vi.stubGlobal("fetch", fetchMock);
  const identity = createIdentityClient("http://identity:8080");
  await identity.POST("/auth/logout", { body: { refreshToken: "r" } });
  await identity.POST("/auth/logout", { body: { refreshToken: "r" }, headers: { "idempotency-key": "k" } });
  await identity.GET("/.well-known/jwks.json");
  const [post, fixed, get] = fetchMock.mock.calls.map(([req]) => req as Request);
  expect(post?.url).toBe("http://identity:8080/auth/logout");
  expect(post?.headers.get("idempotency-key")).toMatch(/^[0-9a-f-]{36}$/);
  expect(fixed?.headers.get("idempotency-key")).toBe("k");
  expect(get?.headers.has("idempotency-key")).toBe(false);
});
