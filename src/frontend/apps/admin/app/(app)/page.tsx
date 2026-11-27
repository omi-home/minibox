import { EmptyState, PageHeader } from "@minibox/ui";

/**
 * Trang tổng quan của app admin; số liệu theo quyền (BRD §13) thêm khi các phân hệ có dữ liệu.
 *
 * @returns Nội dung trang tổng quan.
 */
const Home = () => {
  return (
    <div className="flex w-full flex-col gap-6 px-4 py-6 sm:px-6 lg:px-8">
      <PageHeader title="Tổng quan" description="Việc cần xử lý hôm nay theo quyền của bạn." />
      <EmptyState title="Chưa có số liệu" description="Đơn cần xử lý, hàng sắp hết và việc chờ duyệt sẽ hiện ở đây khi các phân hệ được bật." />
    </div>
  );
};

export default Home;
