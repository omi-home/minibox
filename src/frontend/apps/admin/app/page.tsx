import { AppHeader, EmptyState } from "./auth-forms";

/**
 * Bảng điều khiển admin (chỉ vào được khi đã qua MFA, proxy chặn); chưa có nội dung, menu theo quyền ở #211.
 *
 * @returns Bảng điều khiển rỗng.
 */
const Home = () => {
  return (
    <div className="min-h-dvh bg-background">
      <AppHeader />
      <main className="mx-auto max-w-6xl space-y-6 px-4 py-8">
        <h1 className="text-2xl font-bold tracking-tight">Bảng điều khiển</h1>
        <EmptyState title="Chưa có dữ liệu">Số liệu kho, đơn hàng và sản xuất sẽ hiện ở đây khi các phân hệ được bật.</EmptyState>
      </main>
    </div>
  );
};

export default Home;
