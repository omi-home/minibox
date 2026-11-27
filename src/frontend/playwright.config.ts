import { randomBytes } from "node:crypto";
import { defineConfig, devices } from "@playwright/test";

const ADMIN = "http://localhost:3101";
const IDENTITY_PORT = 5199;

// E2E (spec §9.3): app admin chạy next dev, BFF gọi identity giả e2e/fake-identity.mjs (dữ liệu trong bộ nhớ, nên không dùng lại server cũ).
export default defineConfig({
  testDir: "e2e",
  timeout: 60_000,
  workers: 1,
  forbidOnly: !!process.env.CI,
  reporter: process.env.CI ? "list" : "line",
  use: { baseURL: ADMIN, trace: "retain-on-failure" },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: [
    {
      command: "node e2e/fake-identity.mjs",
      port: IDENTITY_PORT,
      env: { PORT: String(IDENTITY_PORT) },
    },
    {
      command: "pnpm -C apps/admin exec next dev -p 3101",
      url: `${ADMIN}/login`,
      timeout: 180_000,
      env: {
        IDENTITY_URL: `http://localhost:${IDENTITY_PORT}`,
        ADMIN_SESSION_KEY: randomBytes(32).toString("base64"),
        ADMIN_ORIGIN: ADMIN,
      },
    },
  ],
});
