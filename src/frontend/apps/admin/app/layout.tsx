import { Toaster } from "@minibox/ui";
import { fontVariables } from "@minibox/ui/fonts";
import type { Metadata, Viewport } from "next";
import { cookies } from "next/headers";
import { connection } from "next/server";
import type { ReactNode } from "react";
import { THEME_COOKIE, themeOf } from "../lib/menu";
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
 * @remarks Mọi trang render động để Next gắn nonce CSP của request (proxy.ts) vào script; trang tĩnh build sẵn không có nonce.
 */
const RootLayout = async ({ children }: { children: ReactNode }) => {
  await connection();
  const theme = themeOf((await cookies()).get(THEME_COOKIE)?.value);
  return (
    // data-theme theo cookie để không nháy màu; trang /design và nút đổi giao diện đổi lại phía client nên bỏ cảnh báo lệch khi hydrate.
    <html lang="vi" className={fontVariables} data-theme={theme === "system" ? undefined : theme} suppressHydrationWarning>
      <body>
        {children}
        <Toaster />
      </body>
    </html>
  );
};

export default RootLayout;
