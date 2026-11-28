import type { Metadata } from "next";
import Link from "next/link";
import { getTranslator, localization } from "../lib/i18n";
import { localePath, pageMetadata, siteUrl } from "../lib/seo";

/**
 * Đường dẫn trang chủ theo mọi locale đang bật (chuỗi giao diện luôn có, thiếu key rơi về fallback).
 *
 * @returns `{ <locale>: "/" }` cho {@link pageMetadata} và sitemap.
 */
export const homePaths = async (): Promise<Record<string, string>> =>
  Object.fromEntries((await localization().locales()).map((l) => [l.code, "/"]));

/**
 * Metadata SEO của trang chủ theo locale.
 *
 * @param locale - Locale đã xác thực của trang.
 * @returns hreflang + x-default, canonical tự trỏ, og:locale.
 */
export const homeMetadata = async (locale: string): Promise<Metadata> => pageMetadata({ site: siteUrl(), locale, paths: await homePaths() });

/**
 * Trang chủ shop theo locale: chuỗi lấy từ localization (đã áp fallback), kèm liên kết sang mọi ngôn ngữ đang bật.
 *
 * @param props - `locale` của trang (`vi` ở `/`, locale khác ở `/<locale>`).
 * @returns Nội dung trang chủ.
 */
export const Home = async ({ locale }: { locale: string }) => {
  const [t, locales] = await Promise.all([getTranslator(locale), localization().locales()]);
  return (
    <main>
      <h1>{t("home.title")}</h1>
      <p>{t("home.tagline")}</p>
      <nav aria-label={t("home.languages")}>
        <ul>
          {locales.map((l) => (
            <li key={l.code}>
              <Link href={localePath(l.code, "/")} hrefLang={l.code} lang={l.code} aria-current={l.code === locale ? "page" : undefined}>
                {l.name}
              </Link>
            </li>
          ))}
        </ul>
      </nav>
    </main>
  );
};
