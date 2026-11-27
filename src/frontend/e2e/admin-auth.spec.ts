import { expect, type Page, test } from "@playwright/test";
import { totp } from "./totp.mjs";

const PASSWORD = "Mat-khau-du-dai-1";
const step = () => Math.floor(Date.now() / 30_000);

const login = async (page: Page, identifier: string, password = PASSWORD) => {
  await page.goto("/");
  await expect(page).toHaveURL(/\/login$/);
  await page.getByLabel("Email hoặc tên đăng nhập").fill(identifier);
  await page.getByLabel("Mật khẩu").fill(password);
  await page.getByRole("button", { name: "Đăng nhập" }).click();
};

test("ac208_1 nhân viên mới: đăng nhập → đăng ký MFA → lưu mã khôi phục → xác minh → bảng điều khiển; cookie httpOnly; Secure; SameSite=Lax, không token ở trình duyệt", async ({
  page,
  context,
}) => {
  await login(page, "moi@minibox.vn");
  await expect(page.getByRole("heading", { name: "Bật xác thực hai lớp" })).toBeVisible();
  const secret = (await page.getByLabel("Khóa thiết lập").textContent())!.trim();
  await page.getByLabel("Mã 6 số từ ứng dụng").fill(totp(secret, step()));
  await page.getByRole("button", { name: "Bật xác thực hai lớp" }).click();

  const recovery = page.getByRole("list", { name: "Mã khôi phục" }).getByRole("listitem");
  await expect(recovery).toHaveCount(10);
  const code = (await recovery.first().textContent())!;
  await page.getByRole("button", { name: "Tôi đã lưu mã khôi phục" }).click();
  // Mã TOTP vừa dùng không dùng lại được; dùng mã khôi phục thay vì chờ bước 30 giây kế tiếp.
  await page.getByLabel(/Mã xác thực/).fill(code);
  await page.getByRole("button", { name: "Xác minh" }).click();

  await expect(page.getByRole("heading", { name: "Bảng điều khiển" })).toBeVisible();
  await expect(page).toHaveURL(/\/$/);

  const cookies = await context.cookies();
  expect(cookies).toHaveLength(1);
  expect(cookies[0]).toMatchObject({ name: "__Host-minibox_admin", httpOnly: true, secure: true, sameSite: "Lax", path: "/" });
  expect(await page.evaluate(() => document.cookie)).toBe("");
  const visible = await page.evaluate(() => [document.documentElement.outerHTML, JSON.stringify(localStorage), JSON.stringify(sessionStorage)]);
  for (const t of visible) expect(t).not.toMatch(/accessToken|refreshToken/);
});

test("ac208_1 nhân viên đã bật MFA: đăng nhập → mã TOTP → bảng điều khiển; chưa qua MFA không vào được; đăng xuất về trang đăng nhập", async ({ page }) => {
  await login(page, "cu@minibox.vn");
  await expect(page.getByRole("heading", { name: "Xác thực hai lớp" })).toBeVisible();
  await page.goto("/");
  await expect(page).toHaveURL(/\/mfa$/);

  await page.getByLabel(/Mã xác thực/).fill("000000");
  await page.getByRole("button", { name: "Xác minh" }).click();
  await expect(page.getByText("Mã xác thực không đúng hoặc đã được dùng.")).toBeVisible();

  await page.getByLabel(/Mã xác thực/).fill(totp("JBSWY3DPEHPK3PXP", step()));
  await page.getByRole("button", { name: "Xác minh" }).click();
  await expect(page.getByRole("heading", { name: "Bảng điều khiển" })).toBeVisible();

  await page.goto("/login");
  await expect(page).toHaveURL(/\/$/);
  await page.getByRole("button", { name: "Đăng xuất" }).click();
  await expect(page).toHaveURL(/\/login$/);
  await page.goto("/");
  await expect(page).toHaveURL(/\/login$/);
});

test("sai mật khẩu báo lỗi, ở lại trang đăng nhập", async ({ page }) => {
  await login(page, "cu@minibox.vn", "sai");
  await expect(page.getByText("Sai email/tên đăng nhập hoặc mật khẩu.")).toBeVisible();
  await expect(page).toHaveURL(/\/login$/);
});
