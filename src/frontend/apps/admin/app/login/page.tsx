import { AuthCard, LoginForm } from "../auth-forms";

/**
 * Trang đăng nhập nhân viên (bước mật khẩu).
 *
 * @returns Trang đăng nhập.
 */
const LoginPage = () => (
  <AuthCard title="Đăng nhập" description="Dùng tài khoản nhân viên Minibox. Bước sau cần mã xác thực hai lớp.">
    <LoginForm />
  </AuthCard>
);

export default LoginPage;
