import { execFileSync } from "node:child_process";
import { expect, test } from "@playwright/test";
import { totp } from "../totp.mjs";

// Chạy qua `node scripts/smoke.mjs` (đặt biến SMOKE_*), không chạy trong bộ e2e thường.
const env = (k: string) => process.env[k] ?? (() => { throw new Error(`Thiếu ${k}: chạy bằng node scripts/smoke.mjs`); })();

test("ac217_1 smoke: mọi service trong stack Compose đang chạy và healthy", () => {
  const ps = execFileSync("docker", ["compose", "-p", env("SMOKE_PROJECT"), "ps", "--all", "--format", "json"], { encoding: "utf8" })
    .split("\n").filter(Boolean).map((l) => JSON.parse(l) as { Service: string; State: string; Health: string });
  const state = Object.fromEntries(ps.map((c) => [c.Service, `${c.State}/${c.Health}`]));
  expect(state).toEqual(Object.fromEntries(env("SMOKE_SERVICES").split(",").map((s) => [s, "running/healthy"])));
});

test("ac217_1 smoke: nhân viên đăng nhập admin → thiết lập MFA → bảng điều khiển (identity thật)", async ({ page, context }) => {
  const admin = env("SMOKE_ADMIN_URL");
  await page.goto(`${admin}/`);
  await expect(page).toHaveURL(/\/login$/);
  await page.getByLabel("Email hoặc tên đăng nhập").fill(env("SMOKE_EMAIL"));
  await page.getByLabel("Mật khẩu").fill(env("SMOKE_PASSWORD"));
  await page.getByRole("button", { name: "Đăng nhập" }).click();

  await expect(page).toHaveURL(/\/mfa$/);
  await page.getByRole("button", { name: "Thiết lập ứng dụng xác thực" }).click();
  await page.getByText("Không quét được? Nhập khóa thủ công").click();
  const secret = (await page.getByLabel("Khóa thiết lập").textContent())!.replace(/\s/g, "");
  await page.getByLabel("Mã 6 số từ ứng dụng").fill(totp(secret, Math.floor(Date.now() / 30_000)));
  await page.getByRole("button", { name: "Bật xác thực hai lớp" }).click();

  const recovery = page.getByRole("list", { name: "Mã khôi phục" }).getByRole("listitem");
  await expect(recovery).toHaveCount(10);
  const code = (await recovery.first().textContent())!;
  await page.getByLabel("Tôi đã lưu mã khôi phục ở nơi an toàn").click();
  await page.getByRole("button", { name: "Tiếp tục" }).click();
  // Mã TOTP vừa dùng không dùng lại được; dùng mã khôi phục thay vì chờ bước 30 giây kế tiếp.
  await page.getByLabel(/Mã xác thực/).fill(code);
  await page.getByRole("button", { name: "Xác minh" }).click();

  await expect(page.getByRole("heading", { level: 1, name: "Tổng quan" })).toBeVisible();
  expect((await context.cookies()).map((c) => c.name)).toEqual(["__Host-minibox_admin"]);
});
