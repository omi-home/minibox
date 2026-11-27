import { fontVariables } from "@minibox/ui/fonts";
import type { Metadata, Viewport } from "next";
import type { ReactNode } from "react";
import "./globals.css";

/** Viewport phủ cả tai thỏ/thanh home để component dùng env(safe-area-inset-*). */
export const viewport: Viewport = { width: "device-width", initialScale: 1, viewportFit: "cover" };

/** Metadata mặc định của app quản trị cho nhân viên; chặn bot index. */
export const metadata: Metadata = {
  title: "Minibox Admin",
  robots: { index: false, follow: false },
};

/**
 * Layout gốc của app admin: khung HTML tiếng Việt bao mọi trang, gắn biến font tự host.
 *
 * @param props - Thuộc tính layout; `children` là nội dung trang hiện tại.
 * @returns Khung HTML của trang.
 */
const RootLayout = ({ children }: { children: ReactNode }) => {
  return (
    // data-brand/data-theme do trang /design đặt phía client (pha 1 #448), nên bỏ cảnh báo lệch thuộc tính khi hydrate.
    <html lang="vi" className={fontVariables} suppressHydrationWarning>
      <body>{children}</body>
    </html>
  );
};

export default RootLayout;
