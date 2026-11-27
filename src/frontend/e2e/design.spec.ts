import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";
import { APPS } from "../playwright.config";

// 4 nhóm thiết bị của spec §6.4: <640, 640–1023, 1024–1535, ≥1536.
const breakpoints = [
  { name: "mobile", width: 375, height: 812 },
  { name: "tablet", width: 768, height: 1024 },
  { name: "laptop", width: 1280, height: 800 },
  { name: "desktop", width: 1920, height: 1080 },
] as const;

for (const [app, base] of Object.entries(APPS)) {
  for (const bp of breakpoints) {
    test(`ac231_3 ${app} /design ${bp.name} ${bp.width}px: chụp ảnh, mobile có điều hướng dưới và bảng thành thẻ`, async ({ page }, info) => {
      await page.setViewportSize(bp);
      await page.goto(`${base}/design`);
      const mobile = bp.width < 640;
      const nav = page.getByRole("navigation", { name: "Điều hướng chính" });
      const table = page.getByRole("table", { name: "Tồn kho hộp carton" });
      const cards = page.getByRole("list", { name: "Tồn kho hộp carton" });
      await expect(nav).toBeVisible({ visible: mobile });
      await expect(cards).toBeVisible({ visible: mobile });
      await expect(table).toBeVisible({ visible: !mobile });
      if (mobile) await expect(cards.getByRole("listitem")).toHaveCount(4);
      // Ảnh chụp làm bằng chứng nghiệm thu (test-results/, CI tải lên artifact).
      const shot = await page.screenshot({ path: info.outputPath(`${app}-${bp.name}.png`), fullPage: true, animations: "disabled" });
      await info.attach(`${app}-${bp.name}`, { body: shot, contentType: "image/png" });
    });
  }

  test(`ac231_4 ${app} /design: axe không có lỗi critical/serious`, async ({ page }) => {
    for (const bp of [breakpoints[0], breakpoints[2]]) {
      await page.setViewportSize(bp);
      await page.goto(`${base}/design`);
      await page.getByRole("heading", { level: 1 }).waitFor();
      // Chờ hiệu ứng hiện dần xong để axe đo màu cuối cùng (bỏ qua hiệu ứng lặp vô hạn như spinner).
      await page.waitForFunction(() => document.getAnimations().every((a) => a.playState !== "running" || a.effect?.getTiming().iterations === Infinity));
      const { violations } = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"]).analyze();
      const severe = violations.filter((v) => v.impact === "critical" || v.impact === "serious");
      expect(severe.map((v) => `${bp.name} ${v.id}: ${v.nodes.map((n) => n.target.join(" ")).join(", ")}`)).toEqual([]);
    }
  });
}
