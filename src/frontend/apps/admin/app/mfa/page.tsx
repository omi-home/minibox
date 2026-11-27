import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { identity } from "../../lib/identity";
import { SESSION_COOKIE, unsealSession } from "../../lib/session";
import { AuthCard, MfaEnroll, MfaVerifyStep } from "../auth-forms";

/**
 * Bước hai của đăng nhập: tài khoản chưa bật MFA thì đăng ký (identity sinh secret mới mỗi lần mở trang), đã bật thì xác minh.
 * Proxy đã bảo đảm có phiên mật khẩu chưa qua MFA.
 *
 * @returns Trang đăng ký hoặc xác minh MFA.
 */
const MfaPage = async () => {
  const session = unsealSession((await cookies()).get(SESSION_COOKIE)?.value);
  if (!session) redirect("/login");
  const { data, error } = await identity(session.accessToken).POST("/auth/mfa/enroll");
  if (data)
    return (
      <AuthCard title="Bật xác thực hai lớp">
        <MfaEnroll secret={data.secret} otpauthUri={data.otpauthUri} />
      </AuthCard>
    );
  if ((error as { code?: string } | undefined)?.code !== "Identity.MfaAlreadyEnrolled") redirect("/login");
  return (
    <AuthCard title="Xác thực hai lớp">
      <MfaVerifyStep />
    </AuthCard>
  );
};

export default MfaPage;
