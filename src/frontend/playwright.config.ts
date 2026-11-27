import { defineConfig, devices } from "@playwright/test";

/** Cổng next dev của từng app khi chạy e2e. */
export const APPS = { shop: "http://localhost:3100", admin: "http://localhost:3101" } as const;

// E2E (spec §9.3): mỗi app chạy next dev; test tự chọn app bằng URL tuyệt đối trong APPS.
export default defineConfig({
  testDir: "e2e",
  timeout: 60_000,
  workers: 1,
  forbidOnly: !!process.env.CI,
  reporter: process.env.CI ? "list" : "line",
  use: { trace: "retain-on-failure" },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: Object.entries(APPS).map(([app, url]) => ({
    command: `pnpm -C apps/${app} exec next dev -p ${new URL(url).port}`,
    url: `${url}/design`,
    timeout: 180_000,
    reuseExistingServer: !process.env.CI,
  })),
});
