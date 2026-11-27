/*
 * Font tự host qua next/font (tải lúc build, không gọi Google Fonts lúc chạy). Chỉ import từ layout của app Next.
 * PHA 1 #448: nạp cả 3 font tiêu đề để so sánh phương án; sau khi PO chọn, chỉ giữ font của phương án đó.
 */
import { Archivo, Be_Vietnam_Pro, Bricolage_Grotesque, JetBrains_Mono, Space_Grotesk } from "next/font/google";

const body = Be_Vietnam_Pro({ subsets: ["latin", "vietnamese"], weight: ["400", "500", "600", "700"], variable: "--font-body", display: "swap" });
const code = JetBrains_Mono({ subsets: ["latin", "vietnamese"], variable: "--font-code", display: "swap" });
const brandA = Bricolage_Grotesque({ subsets: ["latin", "vietnamese"], variable: "--font-brand-a", display: "swap" });
const brandB = Space_Grotesk({ subsets: ["latin", "vietnamese"], variable: "--font-brand-b", display: "swap" });
const brandC = Archivo({ subsets: ["latin", "vietnamese"], variable: "--font-brand-c", display: "swap", axes: ["wdth"] });

/** Class gắn lên `<html>` để khai các biến font mà theme.css dùng (`--font-body`, `--font-code`, `--font-brand-a|b|c`). */
export const fontVariables = [body, code, brandA, brandB, brandC].map((f) => f.variable).join(" ");
