import type { Metadata } from "next";
import Link from "next/link";
import { baseMetadata, Shell } from "./shell";

/** Metadata trang 404 (Next tự thêm noindex). */
export const metadata: Metadata = { ...baseMetadata(), title: "Không tìm thấy trang — Minibox" };

/**
 * Trang 404 cho URL không khớp route nào (app có hai root layout `(vi)` và `[locale]` nên không có layout chung).
 *
 * @returns Trang 404 tiếng Việt.
 */
const GlobalNotFound = () => (
  <Shell lang="vi">
    <main>
      <h1>Không tìm thấy trang</h1>
      <Link href="/">Về trang chủ</Link>
    </main>
  </Shell>
);

export default GlobalNotFound;
