import type { Metadata, Viewport } from "next";
import { Be_Vietnam_Pro } from "next/font/google";
import type { ReactNode } from "react";
import "./globals.css";
import { StepUpProvider } from "./step-up";

/** Font thương hiệu (tạm, OPEN-08) như app shop: next/font tự host, không gọi Google Fonts lúc chạy. */
const brand = Be_Vietnam_Pro({ subsets: ["vietnamese", "latin"], weight: ["400", "500", "600", "700"], variable: "--font-brand", display: "swap" });

/** Viewport phủ tai thỏ/thanh home cho component dùng env(safe-area-inset-*). */
export const viewport: Viewport = { width: "device-width", initialScale: 1, viewportFit: "cover" };

/** Metadata mặc định của app quản trị cho nhân viên; chặn bot index. */
export const metadata: Metadata = {
  title: "Minibox Admin",
  robots: { index: false, follow: false },
};

/**
 * Layout gốc của app admin: khung HTML tiếng Việt bao mọi trang.
 *
 * @param props - Thuộc tính layout; `children` là nội dung trang hiện tại.
 * @returns Khung HTML của trang.
 */
const RootLayout = ({ children }: { children: ReactNode }) => {
  return (
    <html lang="vi" className={brand.variable}>
      <body>
        <StepUpProvider>{children}</StepUpProvider>
      </body>
    </html>
  );
};

export default RootLayout;
