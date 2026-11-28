import { type NextRequest, NextResponse } from "next/server";
import { identity, jsonResponse } from "../../../../../lib/identity";
import { readSession, sameOrigin } from "../../../../../lib/session";

/**
 * Bắt đầu đăng ký MFA cho phiên vừa qua bước mật khẩu: identity sinh secret TOTP mới (thay secret đang chờ nếu có).
 *
 * @param request - Request mang cookie phiên.
 * @returns 200 `{ secret, otpauthUri }`; 401 khi chưa đăng nhập; 403 khi sai `Origin`; lỗi của identity dạng `{ code }`
 * (`Identity.MfaAlreadyEnrolled` khi MFA đã bật).
 */
export async function POST(request: NextRequest) {
  if (!sameOrigin(request)) return NextResponse.json({ code: "bad_origin" }, { status: 403 });
  const session = readSession(request);
  if (!session) return NextResponse.json({ code: "unauthenticated" }, { status: 401 });
  return jsonResponse(await identity(session.accessToken).POST("/auth/mfa/enrollment"));
}
