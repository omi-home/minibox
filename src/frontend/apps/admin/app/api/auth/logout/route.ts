import { type NextRequest, NextResponse } from "next/server";
import { identity } from "../../../../lib/identity";
import { clearSession, readSession, sameOrigin } from "../../../../lib/session";

/**
 * Đăng xuất: thu hồi cả phiên refresh token ở identity và xóa cookie.
 *
 * @param request - Request mang cookie phiên.
 * @returns 204 và cookie bị xóa; 403 khi sai `Origin`.
 */
export async function POST(request: NextRequest) {
  if (!sameOrigin(request)) return NextResponse.json({ code: "bad_origin" }, { status: 403 });
  const session = readSession(request);
  // Identity lỗi vẫn xóa cookie: người dùng đã yêu cầu thoát; refresh token sẽ tự hết hạn.
  if (session) await identity().POST("/auth/logout", { body: { refreshToken: session.refreshToken } }).catch(() => undefined);
  return clearSession(new NextResponse(null, { status: 204 }));
}
