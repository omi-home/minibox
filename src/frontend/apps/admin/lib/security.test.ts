// @vitest-environment node
import { NextRequest } from "next/server";
import { expect, test } from "vitest";
import { proxy } from "../proxy";
import { securityHeaders } from "./security";

test("ac208_4 header CSP có nonce, HSTS, frame-ancestors 'none', noindex", () => {
  const h = securityHeaders("abc123", false);
  const csp = h["Content-Security-Policy"];
  expect(csp).toContain("script-src 'self' 'nonce-abc123' 'strict-dynamic'");
  expect(csp).toContain("frame-ancestors 'none'");
  expect(csp).toContain("object-src 'none'");
  expect(csp).not.toContain("unsafe-eval");
  expect(h["Strict-Transport-Security"]).toMatch(/^max-age=\d{8,}; includeSubDomains$/);
  expect(h["X-Robots-Tag"]).toBe("noindex, nofollow");
  expect(csp).toContain("img-src 'self' data: blob:;");
  expect(securityHeaders("n", false, "/design")["Content-Security-Policy"]).toContain("https://images.unsplash.com");
});

test("ac208_4 proxy gắn header bảo mật với nonce mới mỗi request và chuyển nonce cho Next render", async () => {
  const csp = async () => {
    const res = await proxy(new NextRequest("https://admin.minibox.test/"));
    const value = res.headers.get("content-security-policy") ?? "";
    // Next đọc CSP ở header request (x-middleware-request-*) để gắn nonce vào script của nó.
    expect(res.headers.get("x-middleware-request-content-security-policy")).toBe(value);
    expect(res.headers.get("x-robots-tag")).toBe("noindex, nofollow");
    expect(res.headers.get("strict-transport-security")).toBeTruthy();
    return /'nonce-([A-Za-z0-9+/=]+)'/.exec(value)?.[1];
  };
  const [a, b] = [await csp(), await csp()];
  expect(a).toBeTruthy();
  expect(a).not.toBe(b);
});
