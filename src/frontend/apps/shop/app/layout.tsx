import type { Metadata, Viewport } from "next";
import { Be_Vietnam_Pro } from "next/font/google";
import type { ReactNode } from "react";
import "./globals.css";

/** Font thương hiệu (tạm, OPEN-08): next/font tải lúc build và tự host, không gọi Google Fonts lúc chạy. */
const brand = Be_Vietnam_Pro({ subsets: ["vietnamese", "latin"], weight: ["400", "500", "600", "700"], variable: "--font-brand", display: "swap" });

/** Viewport phủ cả tai thỏ/thanh home để component dùng env(safe-area-inset-*). */
export const viewport: Viewport = { width: "device-width", initialScale: 1, viewportFit: "cover" };

/** Metadata mặc định của app cửa hàng cho khách. */
export const metadata: Metadata = {
  title: "Minibox",
};

/**
 * Layout gốc của app shop: khung HTML tiếng Việt bao mọi trang.
 *
 * @param props - Thuộc tính layout; `children` là nội dung trang hiện tại.
 * @returns Khung HTML của trang.
 */
const RootLayout = ({ children }: { children: ReactNode }) => {
  return (
    <html lang="vi" className={brand.variable}>
      <body>{children}</body>
    </html>
  );
};

export default RootLayout;
