import { expect, type Page, test } from "@playwright/test";
import { APPS } from "../playwright.config";
import { totp } from "./totp.mjs";

const PASSWORD = "Mat-khau-du-dai-1";
const step = () => Math.floor(Date.now() / 30_000);

const login = async (page: Page, identifier: string, password = PASSWORD) => {
  await page.goto(`${APPS.admin}/`);
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
  await expect(page).toHaveURL(/\/mfa$/);
  // Chưa bật MFA: nhập mã báo cần thiết lập; tải trang không sinh secret, chỉ sinh khi bấm nút.
  await page.getByLabel(/Mã xác thực/).fill("123456");
  await page.getByRole("button", { name: "Xác minh" }).click();
  await expect(page.getByText(/chưa bật xác thực hai lớp/)).toBeVisible();
  await page.reload();
  await page.getByRole("button", { name: "Thiết lập ứng dụng xác thực" }).click();
  await expect(page.getByRole("heading", { name: "Thiết lập ứng dụng xác thực" })).toBeVisible();
  await expect(page.getByRole("img", { name: "Mã QR thiết lập ứng dụng xác thực" })).toBeVisible();
  await page.getByText("Không quét được? Nhập khóa thủ công").click();
  const secret = (await page.getByLabel("Khóa thiết lập").textContent())!.replace(/\s/g, "");
  await page.getByLabel("Mã 6 số từ ứng dụng").fill(totp(secret, step()));
  await page.getByRole("button", { name: "Bật xác thực hai lớp" }).click();

  const recovery = page.getByRole("list", { name: "Mã khôi phục" }).getByRole("listitem");
  await expect(recovery).toHaveCount(10);
  const code = (await recovery.first().textContent())!;
  await expect(page.getByRole("button", { name: "Tiếp tục" })).toBeDisabled();
  await page.getByLabel("Tôi đã lưu mã khôi phục ở nơi an toàn").click();
  await page.getByRole("button", { name: "Tiếp tục" }).click();
  // Mã TOTP vừa dùng không dùng lại được; dùng mã khôi phục thay vì chờ bước 30 giây kế tiếp.
  await page.getByLabel(/Mã xác thực/).fill(code);
  await page.getByRole("button", { name: "Xác minh" }).click();

  await expect(page.getByRole("heading", { level: 1, name: "Tổng quan" })).toBeVisible();
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
  await page.getByRole("button", { name: "Thiết lập ứng dụng xác thực" }).click();
  await expect(page.getByText(/đã bật xác thực hai lớp/)).toBeVisible();
  await page.goto(`${APPS.admin}/`);
  await expect(page).toHaveURL(/\/mfa$/);

  await page.getByLabel(/Mã xác thực/).fill("000000");
  await page.getByRole("button", { name: "Xác minh" }).click();
  await expect(page.getByText("Mã xác thực không đúng hoặc đã được dùng.")).toBeVisible();

  await page.getByLabel(/Mã xác thực/).fill(totp("JBSWY3DPEHPK3PXP", step()));
  await page.getByRole("button", { name: "Xác minh" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Tổng quan" })).toBeVisible();

  await page.goto(`${APPS.admin}/login`);
  await expect(page).toHaveURL(/\/$/);
  await page.getByRole("button", { name: /^Tài khoản/ }).click();
  await page.getByRole("menuitem", { name: "Đăng xuất" }).click();
  await expect(page).toHaveURL(/\/login$/);
  await page.goto(`${APPS.admin}/`);
  await expect(page).toHaveURL(/\/login$/);
});

test("sai mật khẩu báo lỗi, ở lại trang đăng nhập", async ({ page }) => {
  await login(page, "cu@minibox.vn", "sai");
  await expect(page.getByText("Sai email/tên đăng nhập hoặc mật khẩu.")).toBeVisible();
  await expect(page).toHaveURL(/\/login$/);
});

test("ac208_1 trang đăng nhập đổi ngôn ngữ và giao diện: chuỗi theo bản dịch đã xuất bản, thiếu key thì dùng tiếng Việt", async ({ page }) => {
  await page.goto(`${APPS.admin}/login`);
  await page.getByRole("button", { name: /^Ngôn ngữ và vùng/ }).click();
  await page.getByRole("menuitemradio", { name: /English/ }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Sign in" })).toBeVisible();
  await expect(page.getByLabel("Email or username")).toBeVisible();
  await expect(page.getByLabel("Mật khẩu")).toBeVisible(); // stub chưa có bản en của key này
  await expect(page.locator("html")).toHaveAttribute("lang", "en");

  await page.getByRole("button", { name: /^Giao diện:/ }).click();
  await expect(page.locator("html")).toHaveAttribute("data-theme", /light|dark/);
});