import { defineConfig, devices } from "@playwright/test";

// Smoke trên stack Compose thật (task #219): không dựng server, scripts/smoke.mjs dựng stack và đặt biến SMOKE_*.
export default defineConfig({
  testDir: "e2e/smoke",
  timeout: 60_000,
  workers: 1,
  forbidOnly: !!process.env.CI,
  reporter: "list",
  use: { trace: "retain-on-failure", locale: "vi-VN" },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
});
