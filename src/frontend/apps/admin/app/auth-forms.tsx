"use client";
import { Box, Button, Input } from "@minibox/ui";
import { useRouter } from "next/navigation";
import { type FormEvent, type ReactNode, useState } from "react";

const messages: Record<string, string> = {
  "Identity.InvalidCredentials": "Sai email/tên đăng nhập hoặc mật khẩu.",
  "Identity.LoginThrottled": "Nhập sai nhiều lần, vui lòng thử lại sau ít phút.",
  "Identity.InvalidMfaCode": "Mã xác thực không đúng hoặc đã được dùng.",
  "Identity.MfaNotEnrolled": "Tài khoản chưa bật xác thực hai lớp.",
  unauthenticated: "Phiên đăng nhập đã hết, vui lòng đăng nhập lại.",
};

/**
 * Đổi mã lỗi BFF thành câu tiếng Việt cho người dùng.
 *
 * @param code - Mã lỗi `{ code }` từ BFF.
 * @returns Thông báo lỗi.
 */
export const errorText = (code: unknown) => messages[String(code)] ?? "Có lỗi xảy ra, vui lòng thử lại.";

/**
 * Gửi JSON tới Route Handler của BFF (cùng origin, kèm cookie phiên).
 *
 * @param url - Đường dẫn BFF.
 * @param body - Thân request.
 * @returns `ok` và dữ liệu JSON (rỗng khi 204); khi lỗi `data.code` là mã lỗi.
 */
export const postJson = async <T = Record<string, unknown>,>(url: string, body: unknown = {}) => {
  const res = await fetch(url, { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify(body) });
  const data = (res.status === 204 ? {} : await res.json().catch(() => ({}))) as T & { code?: string };
  return { ok: res.ok, data };
};

/**
 * Khung màn hình xác thực: hộp giữa trang có tiêu đề.
 *
 * @param props - `title` là tiêu đề, `children` là nội dung.
 * @returns Trang xác thực.
 */
export const AuthCard = ({ title, children }: { title: string; children: ReactNode }) => (
  <main className="grid min-h-dvh place-items-center p-4">
    <Box depth={2} className="w-full max-w-sm space-y-4">
      <h1 className="text-xl font-semibold">{title}</h1>
      {children}
    </Box>
  </main>
);

/**
 * Form đăng nhập bằng email/tên đăng nhập và mật khẩu; thành công thì sang bước MFA.
 *
 * @returns Form đăng nhập.
 */
export const LoginForm = () => {
  const router = useRouter();
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const submit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setBusy(true);
    const form = new FormData(e.currentTarget);
    const { ok, data } = await postJson("/api/auth/login", { identifier: form.get("identifier"), password: form.get("password") });
    setBusy(false);
    if (ok) router.replace("/mfa");
    else setError(errorText(data.code));
  };
  return (
    <form onSubmit={submit} className="space-y-4">
      <Input label="Email hoặc tên đăng nhập" name="identifier" autoComplete="username" required />
      <Input label="Mật khẩu" name="password" type="password" autoComplete="current-password" required error={error} />
      <Button type="submit" loading={busy} className="w-full">
        Đăng nhập
      </Button>
    </form>
  );
};

/**
 * Form nhập mã TOTP 6 số hoặc mã khôi phục để xác minh MFA (sau mật khẩu hoặc khi xác thực lại).
 *
 * @param props - `onDone` chạy khi xác minh thành công (phiên đã được thay bằng phiên có MFA); `submitLabel` là chữ trên nút.
 * @returns Form nhập mã.
 */
export const MfaVerifyForm = ({ onDone, submitLabel = "Xác minh" }: { onDone: () => void; submitLabel?: string }) => {
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const submit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setBusy(true);
    const { ok, data } = await postJson("/api/auth/mfa", { code: new FormData(e.currentTarget).get("code") });
    setBusy(false);
    if (ok) onDone();
    else setError(errorText(data.code));
  };
  return (
    <form onSubmit={submit} className="space-y-4">
      <Input
        label="Mã xác thực"
        name="code"
        hint="6 chữ số trong ứng dụng xác thực, hoặc một mã khôi phục"
        autoComplete="one-time-code"
        required
        error={error}
      />
      <Button type="submit" loading={busy} className="w-full">
        {submitLabel}
      </Button>
    </form>
  );
};

/**
 * Đăng ký MFA: hiện secret để thêm vào ứng dụng xác thực, xác nhận bằng mã đầu tiên, hiện mã khôi phục một lần,
 * rồi xác minh bằng mã kế tiếp để mở phiên có MFA.
 *
 * @param props - `secret` (Base32, nhập tay) và `otpauthUri` (mở trực tiếp ứng dụng xác thực trên điện thoại).
 * @returns Các bước đăng ký MFA.
 */
export const MfaEnroll = ({ secret, otpauthUri }: { secret: string; otpauthUri: string }) => {
  const router = useRouter();
  const [codes, setCodes] = useState<string[]>();
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const confirm = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setBusy(true);
    const { ok, data } = await postJson<{ recoveryCodes: string[] }>("/api/auth/mfa/enroll/confirm", {
      code: new FormData(e.currentTarget).get("code"),
    });
    setBusy(false);
    if (ok) setCodes(data.recoveryCodes);
    else setError(errorText(data.code));
  };
  if (codes && saved)
    return (
      <>
        <p className="text-sm">Nhập mã kế tiếp trong ứng dụng xác thực để hoàn tất đăng nhập.</p>
        <MfaVerifyForm onDone={() => router.replace("/")} />
      </>
    );
  if (codes)
    return (
      <>
        <p className="text-sm">
          Lưu các mã khôi phục dưới đây ở nơi an toàn. Mỗi mã dùng được một lần khi mất điện thoại; mã chỉ hiện lần này.
        </p>
        <ul aria-label="Mã khôi phục" className="grid grid-cols-2 gap-2 font-mono text-sm">
          {codes.map((c) => (
            <li key={c}>{c}</li>
          ))}
        </ul>
        <Button className="w-full" onClick={() => setSaved(true)}>
          Tôi đã lưu mã khôi phục
        </Button>
      </>
    );
  return (
    <>
      <p className="text-sm">
        Thêm tài khoản vào ứng dụng xác thực (Google Authenticator, Microsoft Authenticator…) bằng khóa dưới đây, hoặc{" "}
        <a className="underline" href={otpauthUri}>
          mở trên điện thoại
        </a>
        .
      </p>
      <p aria-label="Khóa thiết lập" className="break-all rounded-box bg-surface-sunken p-3 font-mono text-sm">
        {secret}
      </p>
      <form onSubmit={confirm} className="space-y-4">
        <Input label="Mã 6 số từ ứng dụng" name="code" inputMode="numeric" autoComplete="one-time-code" required error={error} />
        <Button type="submit" loading={busy} className="w-full">
          Bật xác thực hai lớp
        </Button>
      </form>
    </>
  );
};

/**
 * Bước xác minh MFA sau mật khẩu cho tài khoản đã bật MFA; xong thì vào bảng điều khiển.
 *
 * @returns Form xác minh.
 */
export const MfaVerifyStep = () => {
  const router = useRouter();
  return <MfaVerifyForm onDone={() => router.replace("/")} />;
};

/**
 * Nút đăng xuất: thu hồi phiên ở BFF rồi về trang đăng nhập.
 *
 * @returns Nút đăng xuất.
 */
export const LogoutButton = () => {
  const router = useRouter();
  return (
    <Button
      variant="secondary"
      onClick={async () => {
        await postJson("/api/auth/logout");
        router.replace("/login");
      }}
    >
      Đăng xuất
    </Button>
  );
};
