import { notFound } from "next/navigation";
import { pageLocale } from "../../lib/i18n";
import { Home } from "../home";

/**
 * Trang chủ của locale khác `vi` ở `/<locale>`; locale chưa bật hoặc `vi` (đã ở gốc) thì 404.
 *
 * @param props - `params.locale` là tiền tố đường dẫn.
 * @returns Trang chủ theo locale.
 */
const LocalePage = async ({ params }: { params: Promise<{ locale: string }> }) => {
  const { locale: prefix } = await params;
  const locale = await pageLocale(prefix);
  if (locale === "vi" || locale !== prefix) notFound();
  return <Home locale={locale} />;
};

export default LocalePage;
