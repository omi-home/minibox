"use client";
import {
  CheckCircleIcon,
  CopyIcon,
  DeviceMobileIcon,
  DownloadSimpleIcon,
  FactoryIcon,
  KeyIcon,
  LockKeyIcon,
  PackageIcon,
  ReceiptIcon,
  ShieldCheckIcon,
  SignOutIcon,
  WarehouseIcon,
} from "@phosphor-icons/react";
import { Box, Button, Checkbox, Input } from "@minibox/ui";
import { useRouter } from "next/navigation";
import { QRCodeSVG } from "qrcode.react";
import { type FormEvent, type ReactNode, useState } from "react";
import { BrandArt } from "./brand-art";

const highlights = [
  { icon: WarehouseIcon, text: "Tồn kho theo từng kho, nhập xuất và kiểm kê" },
  { icon: ReceiptIcon, text: "Đơn hàng, thanh toán và giao vận" },
  { icon: FactoryIcon, text: "Kế hoạch sản xuất và kiểm tra chất lượng" },
];

const messages: Record<string, string> = {
  "Identity.InvalidCredentials": "Sai email/tên đăng nhập hoặc mật khẩu. Kiểm tra lại rồi thử lần nữa.",
  "Identity.LoginThrottled": "Nhập sai nhiều lần. Vui lòng đợi ít phút rồi thử lại.",
  "Identity.InvalidMfaCode": "Mã xác thực không đúng hoặc đã được dùng.",
  "Identity.MfaNotEnrolled": "Tài khoản chưa bật xác thực hai lớp. Bấm “Thiết lập ứng dụng xác thực” bên dưới.",
  "Identity.MfaAlreadyEnrolled": "Tài khoản đã bật xác thực hai lớp. Nhập mã từ ứng dụng xác thực hoặc mã khôi phục.",
  unauthenticated: "Phiên đăng nhập đã hết. Vui lòng đăng nhập lại.",
};

/**
 * Đổi mã lỗi BFF thành câu tiếng Việt cho người dùng.
 *
 * @param code - Mã lỗi `{ code }` từ BFF.
 * @returns Thông báo lỗi.
 */
export const errorText = (code: unknown) => messages[String(code)] ?? "Có lỗi xảy ra. Vui lòng thử lại.";

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

/** Logo chữ của app admin: khối hộp + tên. */
const Brand = ({ inverted }: { inverted?: boolean }) => (
  <div className="flex items-center gap-2.5">
    <span
      className={
        inverted
          ? "grid size-10 place-items-center rounded-box bg-primary-foreground text-primary shadow-box-1"
          : "grid size-10 place-items-center rounded-box bg-primary text-primary-foreground shadow-key [--key-edge:var(--color-primary-edge)]"
      }
    >
      <PackageIcon aria-hidden weight="fill" className="size-6" />
    </span>
    <span className="text-lg font-bold tracking-tight">
      Minibox <span className="font-medium opacity-80">Admin</span>
    </span>
  </div>
);

/**
 * Khung màn hình xác thực: panel thương hiệu bên trái (từ 1024px), form bên phải; mobile một cột có logo trên đầu.
 *
 * @param props - `title` là tiêu đề trang (h1), `description` là câu dẫn dưới tiêu đề, `children` là nội dung.
 * @returns Trang xác thực.
 */
export const AuthCard = ({ title, description, children }: { title: string; description?: ReactNode; children: ReactNode }) => (
  <div className="grid min-h-dvh bg-background lg:grid-cols-[minmax(0,5fr)_minmax(0,6fr)]">
    <aside className="relative hidden overflow-hidden p-12 text-primary-foreground lg:flex lg:flex-col lg:justify-between xl:p-16">
      <BrandArt />
      <div className="relative">
        <Brand inverted />
      </div>
      <div className="relative max-w-md space-y-6">
        <p className="text-4xl leading-tight font-bold tracking-tight text-balance">Quản lý kho, đơn hàng và sản xuất ở một nơi.</p>
        <ul className="space-y-3 text-sm">
          {highlights.map(({ icon: Icon, text }) => (
            <li key={text} className="flex items-center gap-3">
              <span className="grid size-8 shrink-0 place-items-center rounded-box bg-primary-foreground/15 backdrop-blur-sm">
                <Icon aria-hidden className="size-4.5" />
              </span>
              {text}
            </li>
          ))}
        </ul>
        <p className="flex items-center gap-2 border-t border-primary-foreground/20 pt-5 text-xs opacity-80">
          <LockKeyIcon aria-hidden className="size-4" />
          Chỉ dành cho nhân viên Minibox. Mọi lần đăng nhập và thao tác nhạy cảm đều được ghi nhận.
        </p>
      </div>
    </aside>
    <main className="relative flex flex-col items-center justify-center px-4 py-10 sm:px-8">
      <div aria-hidden className="pointer-events-none absolute inset-0 bg-[radial-gradient(var(--color-border)_1px,transparent_1.5px)] bg-size-[22px_22px] [mask-image:radial-gradient(60%_50%_at_50%_50%,transparent_40%,black)] lg:hidden" />
      <div className="relative w-full max-w-sm space-y-8">
        <div className="lg:hidden">
          <Brand />
        </div>
        <header className="space-y-1.5">
          <h1 className="text-2xl font-bold tracking-tight">{title}</h1>
          {description && <p className="text-sm text-muted">{description}</p>}
        </header>
        {children}
        <p className="pt-4 text-center text-xs text-muted lg:text-left">© {new Date().getFullYear()} Minibox · Cần hỗ trợ? Liên hệ quản trị hệ thống.</p>
      </div>
    </main>
  </div>
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
    if (ok) return router.replace("/mfa");
    setBusy(false);
    setError(errorText(data.code));
  };
  return (
    <form onSubmit={submit} className="space-y-5">
      <Input label="Email hoặc tên đăng nhập" name="identifier" autoComplete="username" autoFocus required />
      <Input label="Mật khẩu" name="password" type="password" autoComplete="current-password" required error={error} />
      <Button type="submit" loading={busy} className="w-full">
        Đăng nhập
      </Button>
    </form>
  );
};

const codeInput = "h-14 text-center font-mono text-2xl tracking-[0.3em] placeholder:tracking-[0.3em] placeholder:text-muted/50";

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
    if (ok) return onDone();
    setBusy(false);
    setError(errorText(data.code));
  };
  return (
    <form onSubmit={submit} className="space-y-5">
      <Input
        label="Mã xác thực"
        name="code"
        hint="6 chữ số trong ứng dụng xác thực, hoặc một mã khôi phục."
        autoComplete="one-time-code"
        placeholder="000000"
        autoFocus
        required
        error={error}
        className={codeInput}
      />
      <Button type="submit" loading={busy} className="w-full">
        {submitLabel}
      </Button>
    </form>
  );
};

const steps = ["Quét mã", "Lưu mã khôi phục", "Xác minh"];

/** Thanh tiến trình 3 bước đăng ký MFA. */
const Stepper = ({ current }: { current: number }) => (
  <ol aria-label="Các bước thiết lập" className="grid grid-cols-3 gap-2">
    {steps.map((s, i) => (
      <li key={s} aria-current={i === current ? "step" : undefined} className="space-y-1.5">
        <div className={`h-1.5 rounded-full ${i <= current ? "bg-primary" : "bg-border"}`} />
        <p className={`text-xs ${i === current ? "font-semibold text-foreground" : "text-muted"}`}>
          {i + 1}. {s}
        </p>
      </li>
    ))}
  </ol>
);

/** Nút sao chép văn bản, đổi nhãn khi đã chép. */
const CopyButton = ({ text, label }: { text: string; label: string }) => {
  const [done, setDone] = useState(false);
  return (
    <Button
      variant="secondary"
      className="flex-1"
      onClick={async () => {
        await navigator.clipboard.writeText(text);
        setDone(true);
        setTimeout(() => setDone(false), 2000);
      }}
    >
      {done ? <CheckCircleIcon aria-hidden /> : <CopyIcon aria-hidden />}
      {done ? "Đã sao chép" : label}
    </Button>
  );
};

/**
 * Đăng ký MFA theo 3 bước: quét QR (hoặc nhập khóa) và nhập mã đầu tiên → lưu mã khôi phục (hiện một lần) → xác minh bằng mã
 * kế tiếp để mở phiên có MFA.
 *
 * @param props - `secret` (Base32, nhập tay) và `otpauthUri` (nội dung QR cho ứng dụng xác thực).
 * @returns Các bước đăng ký MFA.
 */
export const MfaEnroll = ({ secret, otpauthUri }: { secret: string; otpauthUri: string }) => {
  const router = useRouter();
  const [codes, setCodes] = useState<string[]>();
  const [saved, setSaved] = useState(false);
  const [next, setNext] = useState(false);
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

  if (codes && next)
    return (
      <div className="space-y-6">
        <Stepper current={2} />
        <p className="text-sm">Nhập mã mới trong ứng dụng xác thực (đợi mã đổi) hoặc một mã khôi phục để hoàn tất đăng nhập.</p>
        <MfaVerifyForm onDone={() => router.replace("/")} />
      </div>
    );

  if (codes) {
    const text = codes.join("\n");
    return (
      <div className="space-y-6">
        <Stepper current={1} />
        <div className="flex gap-3 rounded-box border border-warning/40 bg-warning/10 p-3 text-sm">
          <KeyIcon aria-hidden className="mt-0.5 size-5 shrink-0 text-warning" />
          <p>
            Mỗi mã khôi phục dùng được <strong>một lần</strong> khi mất điện thoại. Mã chỉ hiện lần này — hãy lưu ở nơi an toàn.
          </p>
        </div>
        <ul aria-label="Mã khôi phục" className="grid grid-cols-2 gap-x-4 gap-y-2 rounded-box bg-surface-sunken p-4 font-mono text-sm shadow-inset">
          {codes.map((c) => (
            <li key={c} className="tabular-nums">
              {c}
            </li>
          ))}
        </ul>
        <div className="flex gap-3">
          <CopyButton text={text} label="Sao chép" />
          <Button variant="secondary" className="flex-1" asChild>
            <a href={`data:text/plain;charset=utf-8,${encodeURIComponent(text + "\n")}`} download="minibox-ma-khoi-phuc.txt">
              <DownloadSimpleIcon aria-hidden />
              Tải xuống
            </a>
          </Button>
        </div>
        <Checkbox label="Tôi đã lưu mã khôi phục ở nơi an toàn" checked={saved} onCheckedChange={(v) => setSaved(v === true)} />
        <Button className="w-full" disabled={!saved} onClick={() => setNext(true)}>
          Tiếp tục
        </Button>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <Stepper current={0} />
      <div className="flex flex-col items-center gap-4 sm:flex-row sm:items-start">
        {/* QR luôn đen trên trắng (kể cả dark mode) để ứng dụng quét được. */}
        <div className="shrink-0 rounded-lg border border-border bg-white p-3 shadow-box-1">
          <QRCodeSVG value={otpauthUri} size={152} marginSize={0} title="Mã QR thiết lập ứng dụng xác thực" />
        </div>
        <ol className="space-y-2 text-sm">
          <li className="flex gap-2">
            <DeviceMobileIcon aria-hidden className="mt-0.5 size-5 shrink-0 text-primary" />
            Mở ứng dụng xác thực (Google Authenticator, Microsoft Authenticator…) trên điện thoại.
          </li>
          <li className="flex gap-2">
            <ShieldCheckIcon aria-hidden className="mt-0.5 size-5 shrink-0 text-primary" />
            Quét mã QR, rồi nhập mã 6 số ứng dụng hiển thị.
          </li>
        </ol>
      </div>
      <details className="group rounded-box border border-border px-3 py-2 text-sm">
        <summary className="cursor-pointer py-1 font-medium">Không quét được? Nhập khóa thủ công</summary>
        <div className="mt-2 flex items-center gap-2">
          <code aria-label="Khóa thiết lập" className="flex-1 font-mono text-sm break-all tabular-nums">
            {secret.match(/.{1,4}/g)?.join(" ")}
          </code>
          <CopyButton text={secret} label="Chép" />
        </div>
      </details>
      <form onSubmit={confirm} className="space-y-5">
        <Input
          label="Mã 6 số từ ứng dụng"
          name="code"
          inputMode="numeric"
          autoComplete="one-time-code"
          placeholder="000000"
          maxLength={6}
          required
          error={error}
          className={codeInput}
        />
        <Button type="submit" loading={busy} className="w-full">
          Bật xác thực hai lớp
        </Button>
      </form>
    </div>
  );
};

/**
 * Bước hai của đăng nhập: nhập mã MFA; lần đầu (chưa bật MFA) bấm nút thiết lập để identity sinh secret.
 * Secret chỉ sinh khi người dùng bấm (POST), không sinh khi tải trang, nên F5 hay điều hướng từ site khác không đổi
 * secret đang chờ.
 *
 * @returns Form xác minh kèm nút thiết lập, hoặc các bước đăng ký MFA sau khi bấm.
 */
export const MfaStep = () => {
  const router = useRouter();
  const [enrollment, setEnrollment] = useState<{ secret: string; otpauthUri: string }>();
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const start = async () => {
    setBusy(true);
    const { ok, data } = await postJson<{ secret: string; otpauthUri: string }>("/api/auth/mfa/enroll");
    setBusy(false);
    if (ok) setEnrollment(data);
    else setError(errorText(data.code));
  };
  if (enrollment)
    return (
      <section aria-labelledby="enroll-title" className="space-y-4">
        <h2 id="enroll-title" className="text-lg font-semibold">
          Thiết lập ứng dụng xác thực
        </h2>
        <MfaEnroll secret={enrollment.secret} otpauthUri={enrollment.otpauthUri} />
      </section>
    );
  return (
    <div className="space-y-6">
      <MfaVerifyForm onDone={() => router.replace("/")} />
      <div className="relative text-center text-xs text-muted before:absolute before:inset-x-0 before:top-1/2 before:border-t before:border-border">
        <span className="relative bg-background px-3">Lần đầu đăng nhập?</span>
      </div>
      <div className="space-y-2">
        <Button variant="secondary" loading={busy} className="w-full" onClick={start}>
          <DeviceMobileIcon aria-hidden />
          Thiết lập ứng dụng xác thực
        </Button>
        {error && (
          <p role="alert" className="text-sm text-danger">
            {error}
          </p>
        )}
      </div>
    </div>
  );
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
      variant="ghost"
      onClick={async () => {
        await postJson("/api/auth/logout");
        router.replace("/login");
      }}
    >
      <SignOutIcon aria-hidden />
      Đăng xuất
    </Button>
  );
};

/**
 * Thanh trên cùng của app admin: logo và nút đăng xuất.
 *
 * @returns Header app.
 */
export const AppHeader = () => (
  <header className="border-b border-border bg-surface">
    <div className="mx-auto flex h-16 max-w-6xl items-center justify-between gap-4 px-4">
      <Brand />
      <LogoutButton />
    </div>
  </header>
);

/**
 * Trạng thái rỗng có biểu tượng hộp, dùng cho bảng điều khiển chưa có dữ liệu.
 *
 * @param props - `title` và `children` (mô tả).
 * @returns Hộp trạng thái rỗng.
 */
export const EmptyState = ({ title, children }: { title: string; children: ReactNode }) => (
  <Box className="flex flex-col items-center gap-3 py-12 text-center">
    <span className="grid size-14 place-items-center rounded-lg bg-accent text-primary">
      <PackageIcon aria-hidden className="size-8" />
    </span>
    <h2 className="text-lg font-semibold">{title}</h2>
    <p className="max-w-sm text-sm text-muted">{children}</p>
  </Box>
);
