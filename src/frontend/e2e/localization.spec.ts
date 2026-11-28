import { expect, test } from "@playwright/test";
import { APPS, LOCALIZATION_STUB } from "../playwright.config";
import { signIn } from "./session";

// CR #387 / #395: đổi vi/en ở shop và admin; thêm locale thứ ba qua admin không cần deploy; thiếu key rơi về vi.
// Chuỗi và ngôn ngữ đi qua BFF thật của hai app tới stub localization (e2e/localization-stub.mjs).
test.describe.configure({ mode: "serial" });
test.beforeAll(async ({ request }) => {
  await request.post(`${LOCALIZATION_STUB}/__reset`);
});

const LOCALIZATION_ADMIN = { "*": ["localization.message.manage", "localization.message.publish", "localization.locale.manage"] };

test("shop: chuyển vi ↔ en bằng URL /en; key thiếu ở en hiện bản vi", async ({ page }) => {
  await page.goto(`${APPS.shop}/`);
  await expect(page.getByRole("heading", { level: 1 })).toHaveText("Chào mừng đến Minibox");
  const languages = page.getByRole("navigation", { name: "Ngôn ngữ" });
  await expect(languages.getByRole("link")).toHaveText(["Tiếng Việt", "English"]);

  await languages.getByRole("link", { name: "English" }).click();
  await expect(page).toHaveURL(`${APPS.shop}/en`);
  await expect(page.getByRole("heading", { level: 1 })).toHaveText("Welcome to Minibox");
  await expect(page.getByText("Đồ in 3D theo yêu cầu")).toBeVisible(); // home.tagline chưa dịch en → vi

  await page.getByRole("navigation", { name: "Languages" }).getByRole("link", { name: "Tiếng Việt" }).click();
  await expect(page).toHaveURL(`${APPS.shop}/`);
  await expect(page.getByRole("heading", { level: 1 })).toHaveText("Chào mừng đến Minibox");

  // Locale chưa có/chưa bật và /vi (vi ở gốc) không có trang riêng.
  expect((await page.goto(`${APPS.shop}/ja`))?.status()).toBe(404);
  expect((await page.goto(`${APPS.shop}/vi`))?.status()).toBe(404);
});

test("admin: chọn English ở thanh đầu đổi chuỗi; key thiếu ở en hiện bản vi", async ({ page }) => {
  await signIn(page, LOCALIZATION_ADMIN);
  await page.goto(`${APPS.admin}/`);
  await expect(page.getByRole("heading", { level: 1 })).toHaveText("Tổng quan");

  await page.getByRole("button", { name: /^Ngôn ngữ và vùng/ }).click();
  await page.getByRole("menuitemradio", { name: /English/ }).click();
  await expect(page.getByRole("heading", { level: 1 })).toHaveText("Dashboard");
  await expect(page.getByText("Việc cần xử lý hôm nay theo quyền của bạn.")).toBeVisible(); // dashboard.description chưa dịch en

  await page.reload(); // lựa chọn nhớ bằng cookie
  await expect(page.getByRole("heading", { level: 1 })).toHaveText("Dashboard");
  await page.getByRole("button", { name: /^Ngôn ngữ và vùng/ }).click();
  await page.getByRole("menuitemradio", { name: /Tiếng Việt/ }).click();
  await expect(page.getByRole("heading", { level: 1 })).toHaveText("Tổng quan");
});

test("thêm locale thứ ba (ja, dự phòng en) qua admin: shop /ja và admin dùng được ngay, thiếu key rơi về en rồi vi", async ({ page }) => {
  await signIn(page, LOCALIZATION_ADMIN);
  const table = page.getByRole("table");

  // 1. Thêm ngôn ngữ và bật.
  await page.goto(`${APPS.admin}/cai-dat/ngon-ngu`);
  await page.getByRole("button", { name: "Thêm ngôn ngữ" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel(/Mã ngôn ngữ/).fill("ja");
  await dialog.getByLabel(/Tên hiển thị/).fill("日本語");
  await dialog.getByLabel(/Ngôn ngữ dự phòng/).selectOption("en");
  await dialog.getByRole("button", { name: "Thêm ngôn ngữ" }).click();
  await expect(table.getByText("日本語")).toBeVisible();
  await page.getByRole("switch", { name: "Bật 日本語" }).click();
  await expect(page.getByText("Đã bật 日本語").first()).toBeVisible();

  // 2. Dịch một key của shop sang ja rồi xuất bản.
  await page.goto(`${APPS.admin}/cai-dat/ban-dich?ns=shop`);
  await page.getByRole("button", { name: "Sửa home.title" }).click();
  await page.getByLabel("日本語 (ja)").fill("Minibox へようこそ");
  await page.getByRole("button", { name: "Lưu nháp" }).click();
  await expect(page.getByText("Đã lưu nháp home.title").first()).toBeVisible();
  await page
    .getByRole("list", { name: "Tình trạng bản dịch theo ngôn ngữ" })
    .getByRole("listitem")
    .filter({ hasText: "日本語" })
    .getByRole("button", { name: "Xuất bản" })
    .click();
  await page.getByRole("alertdialog").getByRole("button", { name: "Xuất bản" }).click();
  await expect(page.getByText(/Đã xuất bản 1 chuỗi 日本語/).first()).toBeVisible();

  // 3. Shop có /ja: key đã dịch hiện ja; home.languages chỉ có ở en → en; home.tagline chỉ có ở vi → vi.
  await page.goto(`${APPS.shop}/`);
  await expect(page.getByRole("navigation", { name: "Ngôn ngữ" }).getByRole("link")).toHaveText(["Tiếng Việt", "English", "日本語"]);
  await page.getByRole("link", { name: "日本語" }).click();
  await expect(page).toHaveURL(`${APPS.shop}/ja`);
  await expect(page.getByRole("heading", { level: 1 })).toHaveText("Minibox へようこそ");
  await expect(page.getByRole("navigation", { name: "Languages" })).toBeVisible();
  await expect(page.getByText("Đồ in 3D theo yêu cầu")).toBeVisible();

  // 4. Admin chọn được 日本語; namespace admin chưa dịch ja → en → vi.
  await page.goto(`${APPS.admin}/`);
  await page.getByRole("button", { name: /^Ngôn ngữ và vùng/ }).click();
  await page.getByRole("menuitemradio", { name: /日本語/ }).click();
  await expect(page.getByRole("heading", { level: 1 })).toHaveText("Dashboard");
  await expect(page.getByText("Việc cần xử lý hôm nay theo quyền của bạn.")).toBeVisible();
});
