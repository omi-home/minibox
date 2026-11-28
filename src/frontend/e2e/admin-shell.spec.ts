import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";
import { APPS } from "../playwright.config";
import { signIn as signInWith } from "./session";

// 4 nhóm thiết bị của spec §6.4: <640, 640–1023, 1024–1535, ≥1536.
const breakpoints = [
  { name: "mobile", width: 390, height: 844 },
  { name: "tablet", width: 800, height: 1024 },
  { name: "laptop", width: 1280, height: 800 },
  { name: "desktop", width: 1680, height: 1000 },
] as const;

/** Quyền ở vài vùng để kiểm menu lọc theo quyền. */
const signIn = (page: Page) =>
  signInWith(page, { "*": ["report.dashboard.read", "order.order.confirm", "identity.role.manage", "ops.error.read"], "wh:2": ["inventory.receipt.create"] });

for (const bp of breakpoints) {
  for (const colorScheme of ["light", "dark"] as const) {
    test(`ac208_3 khung admin ${bp.name} ${bp.width}px ${colorScheme}: điều hướng đúng thiết bị, không cuộn ngang, axe sạch`, async ({ page }) => {
      await page.setViewportSize(bp);
      await page.emulateMedia({ colorScheme });
      await signIn(page);
      await page.goto(`${APPS.admin}/`);
      await expect(page.getByRole("heading", { level: 1, name: "Tổng quan" })).toBeVisible();
      await expect(page.getByRole("banner")).toBeVisible();

      const mobile = bp.width < 640;
      const laptopUp = bp.width >= 1024;
      await expect(page.getByRole("navigation", { name: "Điều hướng quản trị" })).toBeVisible({ visible: laptopUp });
      await expect(page.getByRole("navigation", { name: "Điều hướng nhanh" })).toBeVisible({ visible: mobile });
      await expect(page.getByRole("button", { name: "Mở menu" })).toBeVisible({ visible: !mobile && !laptopUp });
      await expect(page.getByRole("button", { name: "Thu gọn thanh bên" })).toBeVisible({ visible: laptopUp });
      // Menu chỉ có mục được quyền (ba vùng), không có mục ngoài quyền.
      if (laptopUp) {
        const sidebar = page.getByRole("navigation", { name: "Điều hướng quản trị" });
        await expect(sidebar.getByRole("heading", { level: 2 })).toHaveText(["Nghiệp vụ", "Cài đặt hệ thống và chính sách", "Kỹ thuật và tích hợp"]);
        await expect(sidebar.getByRole("link", { name: "Tài chính" })).toHaveCount(0);
      }
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);

      const { violations } = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"]).analyze();
      expect(violations.filter((v) => v.impact === "critical" || v.impact === "serious").map((v) => `${v.id}: ${v.nodes.map((n) => n.target).join(", ")}`)).toEqual([]);
    });
  }
}

test("ac208_3 mobile: tab Menu mở sheet menu đầy đủ; laptop: Ctrl+K tìm trang không dấu", async ({ page }) => {
  await signIn(page);
  await page.setViewportSize(breakpoints[0]);
  await page.goto(`${APPS.admin}/`);
  await page.getByRole("button", { name: "Menu", exact: true }).click();
  await expect(page.getByRole("navigation", { name: "Menu đầy đủ" }).getByRole("link")).toHaveCount(6);
  await page.keyboard.press("Escape");

  await page.setViewportSize(breakpoints[2]);
  await page.keyboard.press("Control+k");
  await page.getByRole("combobox").fill("phan quyen");
  await expect(page.getByRole("option")).toHaveText(["Vai trò và phân quyền"]);
});
