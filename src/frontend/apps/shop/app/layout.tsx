import type { Metadata } from "next";
import type { ReactNode } from "react";
import "./globals.css";

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
export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="vi">
      <body>{children}</body>
    </html>
  );
}
