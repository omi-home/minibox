import Link from "next/link";
import { getTranslator, localization } from "../lib/i18n";

/**
 * Trang chủ shop theo locale: chuỗi lấy từ localization (đã áp fallback), kèm liên kết sang mọi ngôn ngữ đang bật.
 *
 * @param props - `locale` của trang (`vi` ở `/`, locale khác ở `/<locale>`).
 * @returns Nội dung trang chủ.
 * @remarks ponytail: chỉ trang chủ; hreflang/canonical/html lang/sitemap làm ở #397.
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
              <Link href={l.code === "vi" ? "/" : `/${l.code}`} hrefLang={l.code} lang={l.code} aria-current={l.code === locale ? "page" : undefined}>
                {l.name}
              </Link>
            </li>
          ))}
        </ul>
      </nav>
    </main>
  );
};
