import { displayPreferences } from "../../lib/i18n";
import { AuthCard, MfaStep } from "../auth-forms";

/**
 * Bước hai của đăng nhập: xác minh MFA, hoặc thiết lập lần đầu. Proxy đã bảo đảm có phiên mật khẩu chưa qua MFA.
 *
 * @returns Trang xác thực hai lớp.
 */
const MfaPage = async () => (
  <AuthCard preferences={await displayPreferences()} title="auth.mfa.title" description="auth.mfa.description">
    <MfaStep />
  </AuthCard>
);

export default MfaPage;
