import { AuthCard, MfaStep } from "../auth-forms";

/**
 * Bước hai của đăng nhập: xác minh MFA, hoặc thiết lập lần đầu. Proxy đã bảo đảm có phiên mật khẩu chưa qua MFA.
 *
 * @returns Trang xác thực hai lớp.
 */
const MfaPage = () => (
  <AuthCard title="Xác thực hai lớp" description="Bảo vệ tài khoản quản trị bằng mã từ ứng dụng xác thực trên điện thoại.">
    <MfaStep />
  </AuthCard>
);

export default MfaPage;
