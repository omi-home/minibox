import { defineConfig, devices } from "@playwright/test";

/** Khóa phiên admin chỉ dùng cho e2e (base64 32 byte), để test tự niêm phong cookie phiên; không dùng ở môi trường thật. */
export const E2E_ADMIN_SESSION_KEY = Buffer.alloc(32, 7).toString("base64");

/** Cổng next dev của từng app khi chạy e2e. */
export const APPS = { shop: "http://localhost:3100", admin: "http://localhost:3101" } as const;

/** Stub service localization (e2e/localization-stub.mjs) mà BFF hai app gọi. */
export const LOCALIZATION_STUB = "http://127.0.0.1:3102";

// E2E (spec §9.3): mỗi app chạy next dev; test tự chọn app bằng URL tuyệt đối trong APPS.
export default defineConfig({
  testDir: "e2e",
  timeout: 60_000,
  workers: 1,
  forbidOnly: !!process.env.CI,
  reporter: process.env.CI ? "list" : "line",
  // Trình duyệt tiếng Việt: admin chọn ngôn ngữ theo Accept-Language khi chưa có cookie.
  use: { trace: "retain-on-failure", locale: "vi-VN" },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: [
    { command: `node e2e/localization-stub.mjs ${new URL(LOCALIZATION_STUB).port}`, url: `${LOCALIZATION_STUB}/locales`, reuseExistingServer: !process.env.CI },
    ...Object.entries(APPS).map(([app, url]) => ({
      command: `pnpm -C apps/${app} exec next dev -p ${new URL(url).port}`,
      url: `${url}/design`,
      timeout: 180_000,
      reuseExistingServer: !process.env.CI,
      // TTL cache 1 ms: thấy ngay ngôn ngữ/chuỗi vừa đổi qua admin (e2e không có RabbitMQ báo MessagesPublished).
      env: {
        LOCALIZATION_URL: LOCALIZATION_STUB,
        LOCALIZATION_CACHE_TTL_MS: "1",
        ...(app === "admin" ? { ADMIN_SESSION_KEY: E2E_ADMIN_SESSION_KEY } : {}),
      },
    })),
  ],
});
