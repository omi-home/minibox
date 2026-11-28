import type { Metadata } from "next";
import { Home, homeMetadata } from "../home";

/**
 * Render mỗi request (SSR) vì chuỗi lấy từ localization lúc chạy; BFF đã cache chuỗi 60 giây và làm mới khi xuất bản.
 *
 * @remarks ponytail: SSR thay vì ISR — build không có LOCALIZATION_URL; chuyển ISR (`revalidate`) khi trang nội dung M1 cần.
 */
export const dynamic = "force-dynamic";

/**
 * SEO của trang chủ tiếng Việt.
 *
 * @returns hreflang/canonical/og:locale của `/`.
 */
export const generateMetadata = (): Promise<Metadata> => homeMetadata("vi");

/**
 * Trang chủ tiếng Việt ở gốc `/`.
 *
 * @returns Trang chủ locale `vi`.
 */
const Page = () => <Home locale="vi" />;

export default Page;
