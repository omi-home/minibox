import { fontVariables } from "@minibox/ui/fonts";
import type { Metadata, Viewport } from "next";
import type { ReactNode } from "react";
import "./globals.css";

/** Viewport phủ cả tai thỏ/thanh home để component dùng env(safe-area-inset-*). */
export const viewport: Viewport = { width: "device-width", initialScale: 1, viewportFit: "cover" };

/** Metadata mặc định của app cửa hàng cho khách. */
export const metadata: Metadata = {
  title: "Minibox",
};

/**
 * Layout gốc của app shop: khung HTML tiếng Việt bao mọi trang, gắn biến font tự host.
 *
 * @param props - Thuộc tính layout; `children` là nội dung trang hiện tại.
 * @returns Khung HTML của trang.
 */
const RootLayout = ({ children }: { children: ReactNode }) => {
  return (
    // data-theme do trang /design đặt phía client (bộ chuyển sáng/tối), nên bỏ cảnh báo lệch thuộc tính khi hydrate.
    <html lang="vi" className={fontVariables} suppressHydrationWarning>
      <body>{children}</body>
    </html>
  );
};

export default RootLayout;
