import type { Metadata } from "next";
import type { ReactNode } from "react";
import { pageLocale } from "../../lib/i18n";
import { baseMetadata, Shell } from "../shell";

export { viewport } from "../shell";

/** Metadata mặc định cho các trang có tiền tố locale. */
export const metadata: Metadata = baseMetadata();

/**
 * Root layout của các trang `/<locale>/…`: `<html lang>` theo locale đã xác thực (tiền tố lạ không vào HTML; trang tự 404).
 *
 * @param props - `params.locale` là tiền tố đường dẫn; `children` là trang hiện tại.
 * @returns Khung HTML đúng ngôn ngữ.
 */
const LocaleLayout = async ({ params, children }: { params: Promise<{ locale: string }>; children: ReactNode }) => {
  const { locale: prefix } = await params;
  return <Shell lang={await pageLocale(prefix)}>{children}</Shell>;
};

export default LocaleLayout;
