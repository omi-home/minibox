import { expect, test } from "@playwright/test";
import { APPS, LOCALIZATION_STUB } from "../playwright.config";

// #397 (CR #387, spec §6.6): SEO đa ngôn ngữ ở shop qua BFF thật + stub localization (vi, en).
test.beforeAll(async ({ request }) => {
  await request.post(`${LOCALIZATION_STUB}/__reset`);
});

// Mặc định không đặt SHOP_PUBLIC_URL nên URL tuyệt đối dùng gốc dev.
const SITE = "http://localhost:3000";

// Đọc HTML do SSR trả (thứ crawler thấy, không chạy JS) rồi phân tích bằng DOMParser của trình duyệt.
const seo = async (page: import("@playwright/test").Page, path: string) => {
  const response = await page.request.get(`${APPS.shop}${path}`);
  const html = await response.text();
  const head = await page.evaluate((source) => {
    const doc = new DOMParser().parseFromString(source, "text/html");
    const attr = (selector: string, name: string) => doc.querySelector(selector)?.getAttribute(name) ?? null;
    return {
      lang: doc.documentElement.getAttribute("lang"),
      canonical: attr('link[rel="canonical"]', "href"),
      robots: doc.querySelectorAll('meta[name="robots"]').length,
      ogLocale: attr('meta[property="og:locale"]', "content"),
      hreflang: Object.fromEntries([...doc.querySelectorAll('link[rel="alternate"][hreflang]')].map((e) => [e.getAttribute("hreflang"), e.getAttribute("href")])),
    };
  }, html);
  return { status: response.status(), ...head };
};

test("html lang, canonical tự trỏ, hreflang hai chiều + x-default", async ({ page }) => {
  const vi = await seo(page, "/");
  const en = await seo(page, "/en");
  const all = { vi: SITE, en: `${SITE}/en`, "x-default": SITE };
  expect(vi).toMatchObject({ status: 200, lang: "vi", canonical: SITE, robots: 0, ogLocale: "vi", hreflang: all });
  expect(en).toMatchObject({ status: 200, lang: "en", canonical: `${SITE}/en`, robots: 0, ogLocale: "en", hreflang: all });
});

test("sitemap theo locale kèm xhtml:link alternate", async ({ request }) => {
  const res = await request.get(`${APPS.shop}/sitemap.xml`);
  expect(res.status()).toBe(200);
  const xml = await res.text();
  expect(xml).toContain('xmlns:xhtml="http://www.w3.org/1999/xhtml"');
  expect(xml.match(/<loc>[^<]+<\/loc>/g)).toEqual([`<loc>${SITE}</loc>`, `<loc>${SITE}/en</loc>`]);
  expect(xml).toContain(`<xhtml:link rel="alternate" hreflang="en" href="${SITE}/en" />`);
});

test("không tự redirect theo Accept-Language/cookie: một URL một ngôn ngữ", async ({ browser }) => {
  const context = await browser.newContext({ locale: "en-US", extraHTTPHeaders: { "accept-language": "en-US,en;q=0.9" } });
  await context.addCookies([{ name: "minibox_locale", value: "en", url: APPS.shop }]);
  const page = await context.newPage();
  const response = await page.goto(`${APPS.shop}/`);
  expect(response?.request().redirectedFrom()).toBeNull();
  await expect(page).toHaveURL(`${APPS.shop}/`);
  await expect(page.locator("html")).toHaveAttribute("lang", "vi");
  await expect(page.getByRole("heading", { level: 1 })).toHaveText("Chào mừng đến Minibox");
  await context.close();
});

test("URL không khớp route trả 404 có noindex", async ({ page }) => {
  const response = await page.goto(`${APPS.shop}/khong/co/trang`);
  expect(response?.status()).toBe(404);
  await expect(page.locator('head meta[name="robots"]')).toHaveAttribute("content", /noindex/);
});
