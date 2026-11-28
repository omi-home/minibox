import { createCipheriv, createDecipheriv, randomBytes } from "node:crypto";
import { type NextRequest, NextResponse } from "next/server";

/** Tên cookie phiên; tiền tố `__Host-` buộc `Secure`, `Path=/`, không `Domain` (cookie riêng của app admin). */
export const SESSION_COOKIE = "__Host-minibox_admin";

/** Phiên BFF: cặp token identity trả về; chỉ nằm phía server, trình duyệt chỉ giữ bản mã hóa. */
export type Session = {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  authMethods: string[];
};

function key(): Buffer {
  const k = Buffer.from(process.env.ADMIN_SESSION_KEY ?? "", "base64");
  if (k.length !== 32) throw new Error("ADMIN_SESSION_KEY phải là base64 của 32 byte");
  return k;
}

/**
 * Mã hóa phiên bằng AES-256-GCM (khóa `ADMIN_SESSION_KEY`) để đặt vào cookie.
 *
 * @param session - Phiên cần mã hóa.
 * @returns Chuỗi base64url gồm iv, tag và bản mã.
 * @throws Error khi thiếu hoặc sai khóa.
 */
export function sealSession(session: Session): string {
  const iv = randomBytes(12);
  const cipher = createCipheriv("aes-256-gcm", key(), iv).setAAD(Buffer.from(SESSION_COOKIE));
  const data = Buffer.concat([cipher.update(JSON.stringify(session), "utf8"), cipher.final()]);
  return Buffer.concat([iv, cipher.getAuthTag(), data]).toString("base64url");
}

/**
 * Giải mã cookie phiên.
 *
 * @param value - Giá trị cookie.
 * @returns Phiên; `undefined` nếu không có cookie, bị sửa hoặc sai khóa.
 */
export function unsealSession(value: string | undefined): Session | undefined {
  if (!value) return undefined;
  try {
    const raw = Buffer.from(value, "base64url");
    const decipher = createDecipheriv("aes-256-gcm", key(), raw.subarray(0, 12)).setAAD(Buffer.from(SESSION_COOKIE));
    decipher.setAuthTag(raw.subarray(12, 28));
    return JSON.parse(Buffer.concat([decipher.update(raw.subarray(28)), decipher.final()]).toString("utf8")) as Session;
  } catch {
    return undefined;
  }
}

/**
 * Đọc phiên từ cookie của request.
 *
 * @param request - Request tới BFF.
 * @returns Phiên hợp lệ hoặc `undefined`.
 */
export function readSession(request: NextRequest): Session | undefined {
  return unsealSession(request.cookies.get(SESSION_COOKIE)?.value);
}

/**
 * Ghi cookie phiên `httpOnly; Secure; SameSite=Lax`, hết hạn cùng refresh token.
 *
 * @param response - Response cần gắn cookie.
 * @param session - Phiên mới.
 * @returns `response` để trả tiếp.
 */
export function writeSession(response: NextResponse, session: Session): NextResponse {
  response.cookies.set(SESSION_COOKIE, sealSession(session), {
    httpOnly: true,
    secure: true,
    sameSite: "lax",
    path: "/",
    expires: new Date(session.refreshTokenExpiresAt),
  });
  return response;
}

/**
 * Xóa cookie phiên.
 *
 * @param response - Response cần xóa cookie.
 * @returns `response` để trả tiếp.
 */
export function clearSession(response: NextResponse): NextResponse {
  response.cookies.set(SESSION_COOKIE, "", { httpOnly: true, secure: true, sameSite: "lax", path: "/", maxAge: 0 });
  return response;
}

/**
 * Chống CSRF cho Route Handler (spec §8.8): request ghi phải có `Origin` trùng origin của app admin.
 *
 * @param request - Request tới Route Handler.
 * @returns `true` nếu là GET/HEAD hoặc `Origin` khớp `ADMIN_ORIGIN` (mặc định origin của request).
 */
export function sameOrigin(request: NextRequest): boolean {
  if (request.method === "GET" || request.method === "HEAD") return true;
  return request.headers.get("origin") === (process.env.ADMIN_ORIGIN ?? request.nextUrl.origin);
}
