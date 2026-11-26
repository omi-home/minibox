import { type NextRequest, NextResponse } from "next/server";
import { needsRefresh, refreshSession } from "./lib/identity";
import { clearSession, readSession, SESSION_COOKIE, sealSession, writeSession } from "./lib/session";

/**
 * Làm mới access token trước khi tới trang/Route Handler: access còn dưới 30 giây thì xoay refresh token, ghi cookie mới cho
 * trình duyệt và cho chính request này; phiên hết hạn/bị thu hồi thì xóa cookie.
 *
 * @param request - Request tới app admin.
 * @returns Response tiếp tục xử lý, có thể kèm cookie phiên mới.
 */
export async function proxy(request: NextRequest) {
  const session = readSession(request);
  if (!session || !needsRefresh(session)) return NextResponse.next();
  const next = await refreshSession(session.refreshToken);
  if (!next) {
    request.cookies.delete(SESSION_COOKIE);
    return clearSession(NextResponse.next({ request: { headers: request.headers } }));
  }
  request.cookies.set(SESSION_COOKIE, sealSession(next));
  return writeSession(NextResponse.next({ request: { headers: request.headers } }), next);
}

/** Bỏ qua file tĩnh của Next. */
export const config = { matcher: ["/((?!_next/static|_next/image|favicon.ico).*)"] };
