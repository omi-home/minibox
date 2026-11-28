import { Home } from "./home";

/**
 * Render mỗi request vì chuỗi lấy từ localization lúc chạy.
 *
 * @remarks ponytail: ISR làm mới khi xuất bản thuộc #397.
 */
export const dynamic = "force-dynamic";

/**
 * Trang chủ tiếng Việt ở gốc `/`.
 *
 * @returns Trang chủ locale `vi`.
 */
const Page = () => <Home locale="vi" />;

export default Page;
