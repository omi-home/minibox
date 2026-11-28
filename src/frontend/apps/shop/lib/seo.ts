import { DEFAULT_LOCALE } from "@minibox/api-clients";
import type { Metadata, MetadataRoute } from "next";

/**
 * Đường dẫn của một trang theo locale (spec §6.6): `vi` ở gốc, locale khác có tiền tố `/<locale>`.
 *
 * @param locale - Mã locale đã xác thực (thuộc danh sách đang bật).
 * @param path - Đường dẫn trong locale đó, bắt đầu bằng `/` (slug đã dịch).
 * @returns Đường dẫn tuyệt đối trong site.
 */
export function localePath(locale: string, path: string): string {
  if (locale === DEFAULT_LOCALE) return path;
  return path === "/" ? `/${locale}` : `/${locale}${path}`;
}

/** Đường dẫn của một trang theo từng locale đã xuất bản bản dịch, ví dụ `{ vi: "/san-pham/coc", en: "/products/mug" }`. */
export type LocalizedPaths = Record<string, string>;

// URL tuyệt đối; trang gốc không có `/` cuối — cùng dạng Next chuẩn hoá trong metadata, để canonical khớp sitemap.
const absolute = (site: string, locale: string, path: string) => site + localePath(locale, path).replace(/^\/$/, "");

const languages = (site: string, paths: LocalizedPaths) =>
  Object.fromEntries(Object.entries(paths).map(([code, path]) => [code, absolute(site, code, path)]));

const ogLocale = (code: string) => code.replace("-", "_");

/**
 * Metadata SEO đa ngôn ngữ của một trang: `hreflang` mọi bản đã xuất bản + `x-default` (bản `vi`), `canonical` tự trỏ,
 * `og:locale`/`og:locale:alternate`; locale đang hiển thị fallback (không có trong `paths`) thì `noindex`.
 *
 * @param input - `site` gốc URL công khai; `locale` của trang đang render (đã xác thực bằng `pageLocale`); `paths` các bản đã
 * xuất bản; `path` đường dẫn của trang hiện tại khi locale không có trong `paths` (trang fallback), mặc định bản `vi`.
 * @returns Phần `alternates`/`robots`/`openGraph` để gộp vào `generateMetadata`.
 */
export function pageMetadata(input: { site: string; locale: string; paths: LocalizedPaths; path?: string }): Metadata {
  const { site, locale, paths } = input;
  const published = locale in paths;
  const url = absolute(site, locale, paths[locale] ?? input.path ?? paths[DEFAULT_LOCALE] ?? "/");
  const alternates = languages(site, paths);
  if (alternates[DEFAULT_LOCALE]) alternates["x-default"] = alternates[DEFAULT_LOCALE];
  return {
    alternates: { canonical: url, languages: alternates },
    openGraph: { url, locale: ogLocale(locale), alternateLocale: Object.keys(paths).filter((c) => c !== locale).map(ogLocale) },
    ...(published ? {} : { robots: { index: false, follow: true } }),
  };
}

/**
 * Mục sitemap: mỗi bản đã xuất bản của mỗi trang là một URL, kèm `xhtml:link` alternate tới mọi bản khác.
 *
 * @param site - Gốc URL công khai.
 * @param pages - Các trang, mỗi trang là đường dẫn theo locale đã xuất bản (locale chưa xuất bản không có mặt).
 * @returns Mảng cho `app/sitemap.ts`.
 */
export function sitemapEntries(site: string, pages: LocalizedPaths[]): MetadataRoute.Sitemap {
  return pages.flatMap((paths) => {
    const alt = languages(site, paths);
    return Object.values(alt).map((url) => ({ url, alternates: { languages: alt } }));
  });
}

/**
 * Xử lý slug đã dịch theo lịch sử: slug hiện hành thì hiển thị, slug cũ thì chuyển hướng vĩnh viễn về slug mới.
 *
 * @param requested - Slug trên URL.
 * @param slugs - `current` slug hiện hành của locale; `history` các slug cũ của cùng locale.
 * @returns `ok`, `{ redirect }` (trang gọi `permanentRedirect` tới slug mới) hoặc `not-found`.
 * @remarks `permanentRedirect` của Next trả 308 — tương đương 301 với công cụ tìm kiếm (giữ thứ hạng); dùng từ M1 (`catalog`).
 */
export function slugTarget(requested: string, slugs: { current: string; history: string[] }): "ok" | "not-found" | { redirect: string } {
  if (requested === slugs.current) return "ok";
  return slugs.history.includes(requested) ? { redirect: slugs.current } : "not-found";
}

/**
 * Gốc URL công khai của shop cho `canonical`/`hreflang`/sitemap (URL tuyệt đối).
 *
 * @returns `SHOP_PUBLIC_URL` bỏ `/` cuối; mặc định `http://localhost:3000` khi dev.
 */
export function siteUrl(): string {
  return (process.env.SHOP_PUBLIC_URL ?? "http://localhost:3000").replace(/\/+$/, "");
}
