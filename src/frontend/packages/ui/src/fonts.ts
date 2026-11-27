/*
 * Font tự host qua next/font (tải lúc build, không gọi Google Fonts lúc chạy). Chỉ import từ layout của app Next.
 * Tiêu đề Space Grotesk, chữ thường Be Vietnam Pro, mono JetBrains Mono; cả ba có subset vietnamese.
 */
import { Be_Vietnam_Pro, JetBrains_Mono, Space_Grotesk } from "next/font/google";

const body = Be_Vietnam_Pro({ subsets: ["latin", "vietnamese"], weight: ["400", "500", "600", "700"], variable: "--font-body", display: "swap" });
const code = JetBrains_Mono({ subsets: ["latin", "vietnamese"], variable: "--font-code", display: "swap" });
const heading = Space_Grotesk({ subsets: ["latin", "vietnamese"], variable: "--font-heading", display: "swap" });

/** Class gắn lên `<html>` để khai các biến font mà theme.css dùng (`--font-body`, `--font-code`, `--font-heading`). */
export const fontVariables = [body, code, heading].map((f) => f.variable).join(" ");
