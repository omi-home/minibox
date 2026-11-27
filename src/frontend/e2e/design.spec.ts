import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";
import { APPS } from "../playwright.config";

// 4 nhóm thiết bị của spec §6.4: <640, 640–1023, 1024–1535, ≥1536.
const breakpoints = [
  { name: "mobile", width: 375, height: 812 },
  { name: "tablet", width: 768, height: 1024 },
  { name: "laptop", width: 1280, height: 800 },
  { name: "desktop", width: 1920, height: 1080 },
] as const;

// Nhóm mục bắt buộc trên /design (spec #448 §7).
const sections = ["Nhận diện", "Nút", "Ô nhập", "Chọn và đánh dấu", "Phản hồi", "Lớp phủ", "Điều hướng", "Thương mại", "Bảng và thẻ", "Bố cục"];

/** Đặt chế độ sáng/tối như bộ chuyển trên trang, chờ hiệu ứng hiện dần xong (bỏ qua hiệu ứng lặp vô hạn). */
const settle = async (page: Page, theme?: string) => {
  await page.evaluate((t) => {
    if (t) document.documentElement.dataset.theme = t;
  }, theme);
  await page.waitForFunction(() => document.getAnimations().every((a) => a.playState !== "running" || a.effect?.getTiming().iterations === Infinity));
};

for (const [app, base] of Object.entries(APPS)) {
  test(`ac231_2 ${app} /design hiển thị đủ nhóm component`, async ({ page }) => {
    await page.goto(`${base}/design`);
    for (const s of sections) await expect(page.getByRole("heading", { level: 2, name: s, exact: true })).toBeVisible();
  });

  for (const bp of breakpoints) {
    test(`ac231_3 ${app} /design ${bp.name} ${bp.width}px: khớp ảnh chuẩn, mobile có điều hướng dưới và bảng thành thẻ`, async ({ page }) => {
      await page.setViewportSize(bp);
      await page.goto(`${base}/design`);
      const mobile = bp.width < 640;
      const nav = page.getByRole("navigation", { name: "Điều hướng chính" });
      const table = page.getByRole("table", { name: "Đơn sản xuất mẫu" });
      const cards = page.getByRole("list", { name: "Đơn sản xuất mẫu" });
      await expect(nav).toBeVisible({ visible: mobile });
      await expect(cards).toBeVisible({ visible: mobile });
      await expect(table).toBeVisible({ visible: !mobile });
      if (mobile) await expect(cards.getByRole("listitem")).toHaveCount(4);
      await settle(page);
      // Ảnh chuẩn (e2e/design.spec.ts-snapshots, chụp trên Linux như CI). Che ảnh sản phẩm mẫu vì tải từ Unsplash.
      // Đổi giao diện có chủ đích: `pnpm exec playwright test --update-snapshots` trong container Playwright rồi commit ảnh mới.
      await expect(page).toHaveScreenshot(`${app}-${bp.name}.png`, { fullPage: true, animations: "disabled", mask: [page.locator("img")], maxDiffPixelRatio: 0.01 });
    });
  }

  test(`ac231_4 ${app} /design: axe không có lỗi critical/serious ở 4 breakpoint × sáng/tối`, async ({ page }) => {
    test.setTimeout(180_000);
    const severe: string[] = [];
    for (const bp of breakpoints) {
      await page.setViewportSize(bp);
      await page.goto(`${base}/design`);
      await page.getByRole("heading", { level: 1 }).waitFor();
      for (const theme of ["light", "dark"]) {
        await settle(page, theme);
        const { violations } = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"]).analyze();
        for (const v of violations.filter((x) => x.impact === "critical" || x.impact === "serious"))
          severe.push(`${bp.name}/${theme} ${v.id}: ${v.nodes.map((n) => n.target.join(" ")).join(", ")}`);
      }
    }
    expect(severe).toEqual([]);
  });
}
