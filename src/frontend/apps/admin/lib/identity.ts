import { NextResponse } from "next/server";
import { clearSession, type Session, writeSession } from "./session";

/**
 * Gọi API phiên của service identity trong mạng nội bộ (`IDENTITY_URL`), kèm `Idempotency-Key` như mọi API ghi.
 *
 * @param path - Đường dẫn, ví dụ `/auth/login`.
 * @param body - Thân JSON.
 * @param bearer - Access token khi endpoint cần xác thực.
 * @returns Response của identity.
 * @throws Error khi thiếu `IDENTITY_URL`.
 */
export function identityPost(path: string, body: unknown, bearer?: string): Promise<Response> {
  const base = process.env.IDENTITY_URL;
  if (!base) throw new Error("Thiếu IDENTITY_URL");
  return fetch(new URL(path, base), {
    method: "POST",
    headers: {
      "content-type": "application/json",
      "idempotency-key": crypto.randomUUID(),
      ...(bearer ? { authorization: `Bearer ${bearer}` } : {}),
    },
    body: JSON.stringify(body),
    cache: "no-store",
  });
}

/**
 * Chuyển response token của identity thành response cho trình duyệt: thành công → 204 + cookie phiên; lỗi → cùng mã HTTP
 * kèm `{ code }`. Token không bao giờ nằm trong thân response.
 *
 * @param res - Response của identity (`TokenResponse` hoặc ProblemDetails).
 * @returns Response cho trình duyệt.
 */
export async function sessionResponse(res: Response): Promise<NextResponse> {
  if (res.ok) return writeSession(new NextResponse(null, { status: 204 }), (await res.json()) as Session);
  const problem = (await res.json().catch(() => ({}))) as { code?: string };
  const response = NextResponse.json({ code: problem.code ?? "unknown" }, { status: res.status });
  return res.status === 401 ? clearSession(response) : response;
}

/**
 * Cho biết access token sắp hết hạn (còn dưới 30 giây) và cần làm mới.
 *
 * @param session - Phiên hiện tại.
 * @param now - Thời điểm hiện tại (ms).
 * @returns `true` nếu cần làm mới.
 */
export function needsRefresh(session: Session, now = Date.now()): boolean {
  return Date.parse(session.accessTokenExpiresAt) - now < 30_000;
}

// ponytail: gom trong một tiến trình; chạy nhiều instance BFF thì cần sticky session hoặc khóa chung.
const inflight = new Map<string, Promise<Session | undefined>>();

/**
 * Đổi refresh token lấy phiên mới, gom các request đồng thời cùng token thành một lần gọi identity.
 *
 * @param refreshToken - Refresh token hiện tại.
 * @returns Phiên mới; `undefined` nếu phiên hết hạn/bị thu hồi hoặc identity lỗi.
 * @remarks Identity coi token đã xoay bị dùng lại là bị đánh cắp và thu hồi cả phiên, nên kết quả được giữ 30 giây cho
 * các request đến muộn còn mang cookie cũ (173.57).
 */
export function refreshSession(refreshToken: string): Promise<Session | undefined> {
  let pending = inflight.get(refreshToken);
  if (!pending) {
    pending = identityPost("/auth/refresh", { refreshToken })
      .then(async (res) => (res.ok ? ((await res.json()) as Session) : undefined))
      .catch(() => undefined);
    inflight.set(refreshToken, pending);
    setTimeout(() => inflight.delete(refreshToken), 30_000).unref?.();
  }
  return pending;
}
