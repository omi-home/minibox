import { type NextRequest, NextResponse } from "next/server";
import { identity, sessionResponse } from "../../../../lib/identity";
import { readSession, sameOrigin } from "../../../../lib/session";

/**
 * Xác minh MFA (yếu tố thứ hai sau mật khẩu, hoặc xác thực lại khi API trả `step_up_required`); phiên cũ bị thay.
 *
 * @param request - Thân JSON `{ code }`: mã TOTP hoặc mã khôi phục.
 * @returns 204 kèm cookie phiên mới; 401 khi chưa đăng nhập; 403 khi sai `Origin`; lỗi của identity dạng `{ code }`.
 */
export async function POST(request: NextRequest) {
  if (!sameOrigin(request)) return NextResponse.json({ code: "bad_origin" }, { status: 403 });
  const session = readSession(request);
  if (!session) return NextResponse.json({ code: "unauthenticated" }, { status: 401 });
  const { code } = (await request.json().catch(() => ({}))) as { code?: string };
  return sessionResponse(
    await identity(session.accessToken).POST("/auth/mfa/verify", { body: { code: code ?? "", refreshToken: session.refreshToken } }),
  );
}
