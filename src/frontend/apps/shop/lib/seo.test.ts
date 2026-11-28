import { expect, test } from "vitest";
import { localePath, pageMetadata, sitemapEntries, slugTarget } from "./seo";

const SITE = "https://shop.example.com";
// Trang sản phẩm có slug dịch: vi + en đã xuất bản, ja đang bật nhưng chưa dịch (hiển thị fallback).
const PATHS = { vi: "/san-pham/coc", en: "/products/mug" };

test("localePath: vi ở gốc, locale khác có tiền tố", () => {
  expect(localePath("vi", "/")).toBe("/");
  expect(localePath("en", "/")).toBe("/en");
  expect(localePath("en", "/products/mug")).toBe("/en/products/mug");
});

test("hreflang hai chiều: mỗi bản trỏ tới mọi bản đã xuất bản + x-default (vi)", () => {
  const vi = pageMetadata({ site: SITE, locale: "vi", paths: PATHS });
  const en = pageMetadata({ site: SITE, locale: "en", paths: PATHS });
  const expected = { vi: `${SITE}/san-pham/coc`, en: `${SITE}/en/products/mug`, "x-default": `${SITE}/san-pham/coc` };
  expect(vi.alternates?.languages).toEqual(expected);
  expect(en.alternates?.languages).toEqual(expected);
});

test("canonical tự trỏ chính nó theo locale; bản đã xuất bản được index", () => {
  const en = pageMetadata({ site: SITE, locale: "en", paths: PATHS });
  expect(en.alternates?.canonical).toBe(`${SITE}/en/products/mug`);
  expect(en.robots).toBeUndefined();
  expect(en.openGraph).toMatchObject({ locale: "en", alternateLocale: ["vi"], url: `${SITE}/en/products/mug` });
});

test("trang fallback (locale chưa có bản dịch) đặt noindex, không vào hreflang", () => {
  const ja = pageMetadata({ site: SITE, locale: "ja", paths: PATHS, path: "/products/mug" });
  expect(ja.robots).toEqual({ index: false, follow: true });
  expect(ja.alternates?.canonical).toBe(`${SITE}/ja/products/mug`);
  expect(Object.keys(ja.alternates?.languages ?? {})).toEqual(["vi", "en", "x-default"]);
});

test("không có bản vi thì không có x-default", () => {
  const md = pageMetadata({ site: SITE, locale: "en", paths: { en: "/x" } });
  expect(md.alternates?.languages).toEqual({ en: `${SITE}/en/x` });
});

test("sitemap: mỗi bản đã xuất bản một URL, kèm alternate mọi locale", () => {
  const entries = sitemapEntries(SITE, [{ vi: "/", en: "/" }, PATHS]);
  expect(entries.map((e) => e.url)).toEqual([SITE, `${SITE}/en`, `${SITE}/san-pham/coc`, `${SITE}/en/products/mug`]);
  for (const e of entries.slice(2)) expect(e.alternates?.languages).toEqual({ vi: `${SITE}/san-pham/coc`, en: `${SITE}/en/products/mug` });
});

test("slug: slug hiện hành → hiển thị; slug cũ trong lịch sử → redirect slug mới; lạ → 404", () => {
  const slugs = { current: "coc-su", history: ["coc", "coc-gom"] };
  expect(slugTarget("coc-su", slugs)).toBe("ok");
  expect(slugTarget("coc", slugs)).toEqual({ redirect: "coc-su" });
  expect(slugTarget("ly", slugs)).toBe("not-found");
});
