import type { Page } from "@playwright/test";
import { SESSION_COOKIE, sealSession } from "../apps/admin/lib/session";
import { E2E_ADMIN_SESSION_KEY } from "../playwright.config";

// sealSession đọc khóa lúc gọi; cùng khóa với admin `next dev` trong webServer.
process.env.ADMIN_SESSION_KEY = E2E_ADMIN_SESSION_KEY;

/**
 * Đăng nhập admin giả: cookie phiên mang access token có `perms` (chữ ký không kiểm ở BFF, chỉ dùng lọc menu).
 *
 * @param page - Trang Playwright.
 * @param perms - Phạm vi → mã quyền.
 */
export const signIn = async (page: Page, perms: Record<string, string[]>) => {
  const far = new Date(Date.now() + 36e5).toISOString();
  const value = sealSession({
    accessToken: `h.${Buffer.from(JSON.stringify({ sub: "e2e", perms })).toString("base64url")}.s`,
    accessTokenExpiresAt: far,
    refreshToken: "e2e",
    refreshTokenExpiresAt: far,
    authMethods: ["pwd", "mfa"],
  });
  await page.context().addCookies([{ name: SESSION_COOKIE, value, domain: "localhost", path: "/", secure: true, httpOnly: true }]);
};
