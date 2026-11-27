/** HSTS 2 năm (chỉ có hiệu lực qua HTTPS; Caddy đứng trước). */
const HSTS = "max-age=63072000; includeSubDomains";

/**
 * Header bảo mật cho mọi response của admin (spec §6.3, §6.6).
 *
 * @param nonce - Nonce ngẫu nhiên của request, Next tự gắn vào script của nó.
 * @param dev - `next dev` cần `unsafe-eval` cho React Refresh.
 * @param path - Đường dẫn request; riêng `/design` (trang demo component) được tải ảnh mẫu từ Unsplash.
 * @returns Tên header → giá trị.
 */
export function securityHeaders(nonce: string, dev: boolean, path = "/"): Record<string, string> & { "Content-Security-Policy": string } {
  const csp = [
    "default-src 'self'",
    `script-src 'self' 'nonce-${nonce}' 'strict-dynamic'${dev ? " 'unsafe-eval'" : ""}`,
    // ponytail: style inline vì React render thuộc tính style (Radix/thư viện UI); siết bằng nonce khi bỏ được style attribute.
    "style-src 'self' 'unsafe-inline'",
    `img-src 'self' data: blob:${path === "/design" ? " https://images.unsplash.com" : ""}`,
    "font-src 'self'",
    "connect-src 'self'",
    "object-src 'none'",
    "base-uri 'none'",
    "form-action 'self'",
    "frame-ancestors 'none'",
  ].join("; ");
  return {
    "Content-Security-Policy": csp,
    "Strict-Transport-Security": HSTS,
    "X-Robots-Tag": "noindex, nofollow",
    "X-Content-Type-Options": "nosniff",
    "Referrer-Policy": "same-origin",
  };
}
