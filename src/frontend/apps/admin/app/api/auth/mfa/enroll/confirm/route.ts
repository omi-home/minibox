import { type NextRequest, NextResponse } from "next/server";
import { identity, jsonResponse } from "../../../../../../lib/identity";
import { readSession, sameOrigin } from "../../../../../../lib/session";

/**
 * Xác nhận đăng ký MFA bằng mã đầu tiên từ ứng dụng xác thực; identity bật MFA và cấp bộ mã khôi phục.
 *
 * @param request - Thân JSON `{ code }`.
 * @returns 200 `{ recoveryCodes }` (chỉ trả một lần); 401 khi chưa đăng nhập; 403 khi sai `Origin`; lỗi của identity dạng `{ code }`.
 */
export async function POST(request: NextRequest) {
  if (!sameOrigin(request)) return NextResponse.json({ code: "bad_origin" }, { status: 403 });
  const session = readSession(request);
  if (!session) return NextResponse.json({ code: "unauthenticated" }, { status: 401 });
  const { code } = (await request.json().catch(() => ({}))) as { code?: string };
  const result = await identity(session.accessToken).POST("/auth/mfa/enrollment/confirm", { body: { code: code ?? "" } });
  return jsonResponse({ ...result, data: result.data && { recoveryCodes: result.data } });
}
