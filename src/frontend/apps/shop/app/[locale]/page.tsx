import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { pageLocale } from "../../lib/i18n";
import { Home, homeMetadata } from "../home";

/** SSR như trang chủ `vi` (xem `app/(vi)/page.tsx`). */
export const dynamic = "force-dynamic";

type Props = { params: Promise<{ locale: string }> };

/** Locale của trang từ tiền tố URL; locale chưa bật hoặc `vi` (đã ở gốc) thì 404. */
const locale = async ({ params }: Props) => {
  const { locale: prefix } = await params;
  const code = await pageLocale(prefix);
  if (code === "vi" || code !== prefix) notFound();
  return code;
};

/**
 * SEO của trang chủ theo locale.
 *
 * @param props - `params.locale` là tiền tố đường dẫn.
 * @returns hreflang/canonical/og:locale của `/<locale>`.
 */
export const generateMetadata = async (props: Props): Promise<Metadata> => homeMetadata(await locale(props));

/**
 * Trang chủ của locale khác `vi` ở `/<locale>`.
 *
 * @param props - `params.locale` là tiền tố đường dẫn.
 * @returns Trang chủ theo locale.
 */
const LocalePage = async (props: Props) => <Home locale={await locale(props)} />;

export default LocalePage;
