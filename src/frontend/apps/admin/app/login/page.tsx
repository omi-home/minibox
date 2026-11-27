import { AuthCard, LoginForm } from "../auth-forms";

/**
 * Trang đăng nhập nhân viên (bước mật khẩu).
 *
 * @returns Trang đăng nhập.
 */
const LoginPage = () => (
  <AuthCard title="Đăng nhập Minibox Admin">
    <LoginForm />
  </AuthCard>
);

export default LoginPage;
