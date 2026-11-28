import { createIdentityClient, type IdentityClient } from "@minibox/api-clients";
import { NextResponse } from "next/server";
import { clearSession, type Session, writeSession } from "./session";
import { STEP_UP_REQUIRED } from "./step-up";

/**
 * Tạo client identity (sinh từ OpenAPI) trỏ tới mạng nội bộ `IDENTITY_URL`; request ghi tự kèm `Idempotency-Key`.
 *
 * @param bearer - Access token khi endpoint cần xác thực.
 * @returns Client identity có kiểu.
 * @throws Error khi thiếu `IDENTITY_URL`.
 */
export function identity(bearer?: string): IdentityClient {
  const base = process.env.IDENTITY_URL;
  if (!base) throw new Error("Thiếu IDENTITY_URL");
  // Response chứa token: không để Next cache fetch (173.81).
  return createIdentityClient(base, {
    fetch: (r) => fetch(r, { cache: "no-store" }),
    ...(bearer ? { headers: { authorization: `Bearer ${bearer}` } } : {}),
  });
}

/**
 * Chuyển kết quả gọi endpoint token của identity thành response cho trình duyệt: thành công → 204 + cookie phiên; lỗi → cùng
 * mã HTTP kèm `{ code }`. Token không bao giờ nằm trong thân response.
 *
 * @param result - Kết quả openapi-fetch: `data` là `TokenResponse`, `error` là ProblemDetails (nếu identity trả JSON).
 * @returns Response cho trình duyệt.
 */
export function sessionResponse(result: { data?: Session; error?: unknown; response: Response }): NextResponse {
  if (result.data) return writeSession(new NextResponse(null, { status: 204 }), result.data);
  return errorResponse(result);
}

/**
 * Chuyển kết quả gọi identity thành JSON cho trình duyệt (dùng cho dữ liệu người dùng được phép thấy, vd. secret khi đăng ký
 * MFA); không cache. Lỗi xử lý như {@link sessionResponse}.
 *
 * @param result - Kết quả openapi-fetch.
 * @returns Response 200 kèm `data`, hoặc lỗi dạng `{ code }`.
 */
export function jsonResponse(result: { data?: unknown; error?: unknown; response: Response }): NextResponse {
  if (result.data !== undefined) return NextResponse.json(result.data, { headers: { "cache-control": "no-store" } });
  return errorResponse(result);
}

function errorResponse(result: { error?: unknown; response: Response }): NextResponse {
  const code = (result.error as { code?: unknown } | undefined)?.code;
  const response = NextResponse.json({ code: typeof code === "string" ? code : "unknown" }, { status: result.response.status });
  // step_up_required: phiên vẫn hợp lệ, chỉ cần xác thực lại MFA (màn hình step-up), không đăng xuất.
  return result.response.status === 401 && code !== STEP_UP_REQUIRED ? clearSession(response) : response;
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
const inflight = new Map<string, Promise<Session | "revoked" | "unavailable">>();

/**
 * Đổi refresh token lấy phiên mới, gom các request đồng thời cùng token thành một lần gọi identity.
 *
 * @param refreshToken - Refresh token hiện tại.
 * @returns Phiên mới; `"revoked"` khi identity từ chối token (hết hạn/bị thu hồi, 4xx); `"unavailable"` khi lỗi mạng hoặc 5xx
 * (giữ phiên cũ, request sau thử lại).
 * @remarks Identity coi token đã xoay bị dùng lại là bị đánh cắp và thu hồi cả phiên, nên kết quả thành công/thu hồi được giữ
 * 30 giây cho các request đến muộn còn mang cookie cũ (173.57).
 */
export function refreshSession(refreshToken: string): Promise<Session | "revoked" | "unavailable"> {
  let pending = inflight.get(refreshToken);
  if (!pending) {
    pending = identity()
      .POST("/auth/refresh", { body: { refreshToken } })
      .then(({ data, response }) => data ?? (response.status < 500 ? ("revoked" as const) : ("unavailable" as const)))
      .catch(() => "unavailable" as const);
    inflight.set(refreshToken, pending);
    void pending.then((r) => {
      if (r === "unavailable") inflight.delete(refreshToken);
      else setTimeout(() => inflight.delete(refreshToken), 30_000).unref?.();
    });
  }
  return pending;
}
