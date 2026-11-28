import { type NextRequest, NextResponse } from "next/server";
import { needsRefresh, refreshSession } from "./lib/identity";
import { securityHeaders } from "./lib/security";
import { clearSession, readSession, SESSION_COOKIE, sealSession, type Session, writeSession } from "./lib/session";

/**
 * Chọn trang phải chuyển tới theo trạng thái phiên: chưa đăng nhập → `/login`, mới qua mật khẩu → `/mfa` (đăng ký hoặc
 * xác minh), đủ MFA thì không vào lại `/login`, `/mfa`. Route Handler `/api/*` tự trả lỗi và trang mẫu `/design` (Caddy
 * production chặn) không cần phiên nên không chuyển.
 *
 * @param path - Đường dẫn của request.
 * @param session - Phiên hiện tại (sau khi làm mới).
 * @returns Đường dẫn cần chuyển tới, hoặc `undefined` nếu được vào.
 */
export function gate(path: string, session: Session | undefined): string | undefined {
  if (path.startsWith("/api/") || path === "/design") return undefined;
  const mfa = session?.authMethods.includes("mfa") ?? false;
  if (path === "/login") return mfa ? "/" : undefined;
  if (path === "/mfa") return !session ? "/login" : mfa ? "/" : undefined;
  return !session ? "/login" : mfa ? undefined : "/mfa";
}

/**
 * Trước mỗi trang/Route Handler: gắn header bảo mật với nonce CSP mới (đưa cả vào header request để Next gắn nonce cho script
 * khi render); access token còn dưới 30 giây thì xoay refresh token, ghi cookie mới cho trình duyệt và cho chính request này
 * (phiên hết hạn/bị thu hồi thì xóa cookie; identity tạm lỗi thì giữ nguyên); rồi chuyển trang theo {@link gate}.
 *
 * @param request - Request tới app admin.
 * @returns Response tiếp tục xử lý hoặc chuyển trang, kèm header bảo mật và có thể kèm cookie phiên mới.
 */
export async function proxy(request: NextRequest) {
  const headers = securityHeaders(
    Buffer.from(crypto.getRandomValues(new Uint8Array(16))).toString("base64"),
    process.env.NODE_ENV === "development",
    request.nextUrl.pathname,
  );
  request.headers.set("content-security-policy", headers["Content-Security-Policy"]);
  let session = readSession(request);
  let changed = false;
  if (session && needsRefresh(session)) {
    const next = await refreshSession(session.refreshToken);
    // Identity tạm lỗi: giữ phiên cũ, không đăng xuất oan (208.1); API phía sau tự trả 401 nếu access đã hết hạn.
    if (next !== "unavailable") {
      changed = true;
      session = next === "revoked" ? undefined : next;
      if (session) request.cookies.set(SESSION_COOKIE, sealSession(session));
      else request.cookies.delete(SESSION_COOKIE);
    }
  }
  const target = gate(request.nextUrl.pathname, session);
  const response = target ? NextResponse.redirect(new URL(target, request.url)) : NextResponse.next({ request: { headers: request.headers } });
  if (changed) {
    if (session) writeSession(response, session);
    else clearSession(response);
  }
  for (const [name, value] of Object.entries(headers)) response.headers.set(name, value);
  return response;
}

/** Bỏ qua file tĩnh của Next. */
export const config = { matcher: ["/((?!_next/static|_next/image|favicon.ico).*)"] };