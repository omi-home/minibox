import type { Metadata } from "next";
import type { ReactNode } from "react";
import "./globals.css";
import { StepUpProvider } from "./step-up";

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
    <html lang="vi">
      <body>
        <StepUpProvider>{children}</StepUpProvider>
      </body>
    </html>
  );
};

export default RootLayout;
