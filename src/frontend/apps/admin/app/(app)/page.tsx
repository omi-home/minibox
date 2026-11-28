import { EmptyState, PageHeader } from "@minibox/ui";
import { getTranslator } from "../../lib/i18n";

/**
 * Trang tổng quan của app admin; số liệu theo quyền (BRD §13) thêm khi các phân hệ có dữ liệu.
 *
 * @returns Nội dung trang tổng quan.
 */
const Home = async () => {
  const t = await getTranslator();
  return (
    <div className="flex w-full flex-col gap-6 px-4 py-6 sm:px-6 lg:px-8">
      <PageHeader title={t("dashboard.title")} description={t("dashboard.description")} />
      <EmptyState title="Chưa có số liệu" description="Đơn cần xử lý, hàng sắp hết và việc chờ duyệt sẽ hiện ở đây khi các phân hệ được bật." />
    </div>
  );
};

export default Home;
