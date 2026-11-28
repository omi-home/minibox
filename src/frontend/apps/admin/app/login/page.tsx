import { displayPreferences } from "../../lib/i18n";
import { AuthCard, LoginForm } from "../auth-forms";

/**
 * Trang đăng nhập nhân viên (bước mật khẩu).
 *
 * @returns Trang đăng nhập.
 */
const LoginPage = async () => (
  <AuthCard preferences={await displayPreferences()} title="auth.login.title" description="auth.login.description">
    <LoginForm />
  </AuthCard>
);

export default LoginPage;
