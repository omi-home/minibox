import { type NextRequest, NextResponse } from "next/server";
import { needsRefresh, refreshSession } from "./lib/identity";
import { securityHeaders } from "./lib/security";
import { clearSession, readSession, SESSION_COOKIE, sealSession, writeSession } from "./lib/session";

/**
 * Trước mỗi trang/Route Handler: gắn header bảo mật với nonce CSP mới (đưa cả vào header request để Next gắn nonce cho script
 * khi render); access token còn dưới 30 giây thì xoay refresh token, ghi cookie mới cho trình duyệt và cho chính request này;
 * phiên hết hạn/bị thu hồi thì xóa cookie.
 *
 * @param request - Request tới app admin.
 * @returns Response tiếp tục xử lý, kèm header bảo mật và có thể kèm cookie phiên mới.
 */
export async function proxy(request: NextRequest) {
  const headers = securityHeaders(
    Buffer.from(crypto.getRandomValues(new Uint8Array(16))).toString("base64"),
    process.env.NODE_ENV === "development",
    request.nextUrl.pathname,
  );
  request.headers.set("content-security-policy", headers["Content-Security-Policy"]);
  const session = readSession(request);
  let response: NextResponse;
  if (!session || !needsRefresh(session)) response = NextResponse.next({ request: { headers: request.headers } });
  else {
    const next = await refreshSession(session.refreshToken);
    if (next) request.cookies.set(SESSION_COOKIE, sealSession(next));
    else request.cookies.delete(SESSION_COOKIE);
    response = NextResponse.next({ request: { headers: request.headers } });
    if (next) writeSession(response, next);
    else clearSession(response);
  }
  for (const [name, value] of Object.entries(headers)) response.headers.set(name, value);
  return response;
}

/** Bỏ qua file tĩnh của Next. */
export const config = { matcher: ["/((?!_next/static|_next/image|favicon.ico).*)"] };
