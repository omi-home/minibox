import { type NextRequest, NextResponse } from "next/server";
import { identity, sessionResponse } from "../../../../lib/identity";
import { sameOrigin } from "../../../../lib/session";

/**
 * Đăng nhập bằng mật khẩu: identity mở phiên `pwd`, BFF đặt cookie phiên; nhân viên còn phải qua `/api/auth/mfa`.
 *
 * @param request - Thân JSON `{ identifier, password }`.
 * @returns 204 kèm cookie phiên; 403 khi sai `Origin`; lỗi của identity dạng `{ code }`.
 */
export async function POST(request: NextRequest) {
  if (!sameOrigin(request)) return NextResponse.json({ code: "bad_origin" }, { status: 403 });
  const { identifier, password } = (await request.json().catch(() => ({}))) as { identifier?: string; password?: string };
  // Caddy ghi đè X-Forwarded-For; lấy phần tử cuối để client không chèn IP giả lên đầu.
  const ip = request.headers.get("x-forwarded-for")?.split(",").at(-1)?.trim() ?? "";
  return sessionResponse(await identity().POST("/auth/login", { body: { identifier: identifier ?? "", password: password ?? "", ip } }));
}
