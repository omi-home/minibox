import { fontVariables } from "@minibox/ui/fonts";
import type { Metadata, Viewport } from "next";
import type { ReactNode } from "react";
import { siteUrl } from "../lib/seo";
import "./globals.css";

/** Viewport phủ cả tai thỏ/thanh home để component dùng env(safe-area-inset-*). */
export const viewport: Viewport = { width: "device-width", initialScale: 1, viewportFit: "cover" };

/**
 * Metadata mặc định của app cửa hàng cho khách; `metadataBase` để URL tương đối thành tuyệt đối.
 *
 * @returns Metadata dùng chung cho hai root layout.
 */
export const baseMetadata = (): Metadata => ({ title: "Minibox", metadataBase: new URL(siteUrl()) });

/**
 * Khung HTML chung của app shop, dùng bởi hai root layout: `(vi)` ở gốc và `[locale]` cho ngôn ngữ khác.
 *
 * @param props - `lang` là locale của trang cho `<html lang>`; `children` là nội dung trang.
 * @returns Khung HTML của trang.
 */
export const Shell = ({ lang, children }: { lang: string; children: ReactNode }) => (
  // data-theme do trang /design đặt phía client (bộ chuyển sáng/tối), nên bỏ cảnh báo lệch thuộc tính khi hydrate.
  <html lang={lang} className={fontVariables} suppressHydrationWarning>
    <body>{children}</body>
  </html>
);
