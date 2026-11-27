import { Box } from "@minibox/ui";
import { LogoutButton } from "./auth-forms";

/**
 * Bảng điều khiển admin (chỉ vào được khi đã qua MFA, proxy chặn); chưa có nội dung, menu theo quyền ở #211.
 *
 * @returns Bảng điều khiển rỗng.
 */
const Home = () => {
  return (
    <main className="mx-auto max-w-5xl space-y-4 p-4">
      <header className="flex items-center justify-between gap-4">
        <h1 className="text-xl font-semibold">Minibox Admin</h1>
        <LogoutButton />
      </header>
      <Box>
        <h2 className="font-semibold">Bảng điều khiển</h2>
        <p className="text-sm text-muted">Chưa có dữ liệu.</p>
      </Box>
    </main>
  );
};

export default Home;
