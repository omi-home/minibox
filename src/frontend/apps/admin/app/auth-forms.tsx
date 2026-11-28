"use client";
import {
  CheckCircleIcon,
  CopyIcon,
  DeviceMobileIcon,
  DownloadSimpleIcon,
  KeyIcon,
  LockKeyIcon,
  PrinterIcon,
  ReceiptIcon,
  ShieldCheckIcon,
  WarehouseIcon,
} from "@phosphor-icons/react";
import { Box, BrandScene, Button, Checkbox, Input, Logo, RegionMenu, type ThemeMode, ThemeToggle } from "@minibox/ui";
import { useRouter } from "next/navigation";
import { QRCodeSVG } from "qrcode.react";
import { type FormEvent, type ReactNode, useState } from "react";
import { LOCALE_COOKIE, THEME_COOKIE } from "../lib/menu";
import { FLAGS } from "./(app)/shell";
import { type AuthTextKey, useAuthText } from "./auth-text";
import { BrandArt, OrbitStage } from "./brand-art";

/** Ba mảng việc chính của hệ thống, theo luồng một đơn hàng in 3D (BRD §6, §9). */
const highlights = [
  { icon: ReceiptIcon, key: "auth.brand.orders" },
  { icon: PrinterIcon, key: "auth.brand.production" },
  { icon: WarehouseIcon, key: "auth.brand.warehouse" },
] as const;

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

/** Lựa chọn hiển thị đọc ở server (`displayPreferences` ở lib/i18n): giao diện, ngôn ngữ hiện tại và các ngôn ngữ đang bật. */
export type AuthPreferences = {
  theme: ThemeMode;
  locale: string;
  locales: { code: string; name: string }[];
};

const remember = (name: string, value: string) => {
  document.cookie = `${name}=${value}; Path=/; Max-Age=31536000; SameSite=Lax`;
};

/**
 * Nút đổi giao diện và chọn ngôn ngữ ở góc trang xác thực, cùng cookie với thanh đầu khung admin nên lựa chọn giữ sau khi đăng nhập.
 * Đổi ngôn ngữ thì server render lại trang với chuỗi của ngôn ngữ mới.
 *
 * @param props - {@link AuthPreferences}.
 * @returns Thanh công cụ nhỏ trên thẻ bề mặt.
 */
const AuthToolbar = ({ theme: initialTheme, locale: initialLocale, locales }: AuthPreferences) => {
  const router = useRouter();
  const [theme, setTheme] = useState(initialTheme);
  const [locale, setLocale] = useState(initialLocale);
  return (
    <div className="flex items-center gap-1 rounded-lg border border-border bg-surface p-1 shadow-box-1">
      <ThemeToggle
        plain
        className="flex"
        value={theme}
        onChange={(mode) => {
          setTheme(mode);
          remember(THEME_COOKIE, mode);
          if (mode === "system") delete document.documentElement.dataset.theme;
          else document.documentElement.dataset.theme = mode;
        }}
      />
      <RegionMenu
        plain
        className="flex"
        locale={{
          value: locale,
          options: locales.map((l) => ({ code: l.code, label: l.name, flag: FLAGS[l.code] })),
          onChange: (code) => {
            setLocale(code);
            remember(LOCALE_COOKIE, code);
            router.refresh();
          },
        }}
      />
    </div>
  );
};

/**
 * Khung màn hình xác thực: panel thương hiệu với hệ hộp quay bên trái (từ 1024px); bên phải là cảnh nền rừng thông của
 * design system, form đặt trên thẻ bề mặt để chữ không nằm trực tiếp trên cảnh. Mobile một cột, logo trên thẻ.
 *
 * @param props - `title`, `description` là key chuỗi của tiêu đề trang (h1) và câu dẫn; `preferences` cho nút giao diện/ngôn ngữ;
 * `children` là nội dung.
 * @returns Trang xác thực.
 */
export const AuthCard = ({
  title,
  description,
  preferences,
  children,
}: {
  title: AuthTextKey;
  description: AuthTextKey;
  preferences: AuthPreferences;
  children: ReactNode;
}) => {
  const t = useAuthText();
  return (
    <div className="grid min-h-dvh lg:grid-cols-[minmax(0,5fr)_minmax(0,6fr)]">
      <BrandScene />
      <aside className="relative hidden overflow-hidden px-12 py-10 text-brand-foreground lg:flex lg:flex-col lg:gap-6 xl:px-16">
        <BrandArt />
        <div className="relative">
          <Logo tone="inverse" className="text-2xl" />
        </div>
        <OrbitStage />
        <div className="relative max-w-md space-y-6">
          <div className="space-y-3">
            <p className="text-xs font-semibold tracking-widest text-brand-muted uppercase">{t("auth.brand.eyebrow")}</p>
            <p className="text-4xl leading-tight font-bold tracking-tight text-balance">{t("auth.brand.title")}</p>
            <p className="text-sm text-brand-muted">{t("auth.brand.lead")}</p>
          </div>
          <ul className="space-y-3 text-sm">
            {highlights.map(({ icon: Icon, key }) => (
              <li key={key} className="flex items-center gap-3">
                <span className="grid size-8 shrink-0 place-items-center rounded-box bg-brand-foreground/12">
                  <Icon aria-hidden className="size-4.5" />
                </span>
                {t(key)}
              </li>
            ))}
          </ul>
          <p className="flex items-center gap-2 border-t border-brand-border pt-5 text-xs text-brand-muted">
            <LockKeyIcon aria-hidden className="size-4 shrink-0" />
            {t("auth.brand.notice")}
          </p>
        </div>
      </aside>
      <main className="relative flex flex-col items-center justify-center px-4 pt-20 pb-10 sm:px-8">
        <div className="absolute top-4 right-4 sm:top-6 sm:right-6">
          <AuthToolbar {...preferences} />
        </div>
        <Box depth={2} className="w-full max-w-md space-y-8 p-6 sm:p-8">
          <div className="lg:hidden">
            <Logo className="text-2xl" />
          </div>
          <header className="space-y-1.5">
            <h1 className="text-2xl font-bold tracking-tight">{t(title)}</h1>
            <p className="text-sm text-muted">{t(description)}</p>
          </header>
          {children}
          <p className="border-t border-border pt-5 text-center text-xs text-muted">{t("auth.footer", { year: new Date().getFullYear() })}</p>
        </Box>
      </main>
    </div>
  );
};

/**
 * Form đăng nhập bằng email/tên đăng nhập và mật khẩu; thành công thì sang bước MFA.
 *
 * @returns Form đăng nhập.
 */
export const LoginForm = () => {
  const t = useAuthText();
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
    setError(t.error(data.code));
  };
  return (
    <form onSubmit={submit} className="space-y-5">
      <Input label={t("auth.login.identifier")} name="identifier" autoComplete="username" autoFocus required />
      <Input label={t("auth.login.password")} name="password" type="password" autoComplete="current-password" required error={error} />
      <Button type="submit" loading={busy} className="w-full">
        {t("auth.login.submit")}
      </Button>
    </form>
  );
};

const codeInput = "h-14 text-center font-mono text-2xl tracking-[0.3em] placeholder:tracking-[0.3em] placeholder:text-muted/50";

/**
 * Form nhập mã TOTP 6 số hoặc mã khôi phục để xác minh MFA (sau mật khẩu hoặc khi xác thực lại).
 *
 * @param props - `onDone` chạy khi xác minh thành công (phiên đã được thay bằng phiên có MFA); `submitLabel` là key chữ trên nút.
 * @returns Form nhập mã.
 */
export const MfaVerifyForm = ({ onDone, submitLabel = "auth.mfa.verify" }: { onDone: () => void; submitLabel?: AuthTextKey }) => {
  const t = useAuthText();
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const submit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setBusy(true);
    const { ok, data } = await postJson("/api/auth/mfa", { code: new FormData(e.currentTarget).get("code") });
    if (ok) return onDone();
    setBusy(false);
    setError(t.error(data.code));
  };
  return (
    <form onSubmit={submit} className="space-y-5">
      <Input
        label={t("auth.mfa.code")}
        name="code"
        hint={t("auth.mfa.codeHint")}
        autoComplete="one-time-code"
        placeholder="000000"
        autoFocus
        required
        error={error}
        className={codeInput}
      />
      <Button type="submit" loading={busy} className="w-full">
        {t(submitLabel)}
      </Button>
    </form>
  );
};

const steps = ["auth.enroll.stepScan", "auth.enroll.stepSave", "auth.enroll.stepVerify"] as const;

/** Thanh tiến trình 3 bước đăng ký MFA. */
const Stepper = ({ current }: { current: number }) => {
  const t = useAuthText();
  return (
    <ol aria-label={t("auth.enroll.steps")} className="grid grid-cols-3 gap-2">
      {steps.map((s, i) => (
        <li key={s} aria-current={i === current ? "step" : undefined} className="space-y-1.5">
          <div className={`h-1.5 rounded-full ${i <= current ? "bg-primary" : "bg-border"}`} />
          <p className={`text-xs ${i === current ? "font-semibold text-foreground" : "text-muted"}`}>
            {i + 1}. {t(s)}
          </p>
        </li>
      ))}
    </ol>
  );
};

/** Nút sao chép văn bản, đổi nhãn khi đã chép. */
const CopyButton = ({ text, label }: { text: string; label: AuthTextKey }) => {
  const t = useAuthText();
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
      {done ? t("auth.recovery.copied") : t(label)}
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
  const t = useAuthText();
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
    else setError(t.error(data.code));
  };

  if (codes && next)
    return (
      <div className="space-y-6">
        <Stepper current={2} />
        <p className="text-sm">{t("auth.enroll.finish")}</p>
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
          <p>{t("auth.recovery.warning")}</p>
        </div>
        <ul aria-label={t("auth.recovery.list")} className="grid grid-cols-2 gap-x-4 gap-y-2 rounded-box bg-surface-sunken p-4 font-mono text-sm shadow-inset">
          {codes.map((c) => (
            <li key={c} className="tabular-nums">
              {c}
            </li>
          ))}
        </ul>
        <div className="flex gap-3">
          <CopyButton text={text} label="auth.recovery.copy" />
          <Button variant="secondary" className="flex-1" asChild>
            <a href={`data:text/plain;charset=utf-8,${encodeURIComponent(text + "\n")}`} download="minibox-recovery-codes.txt">
              <DownloadSimpleIcon aria-hidden />
              {t("auth.recovery.download")}
            </a>
          </Button>
        </div>
        <Checkbox label={t("auth.recovery.saved")} checked={saved} onCheckedChange={(v) => setSaved(v === true)} />
        <Button className="w-full" disabled={!saved} onClick={() => setNext(true)}>
          {t("auth.recovery.continue")}
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
          <QRCodeSVG value={otpauthUri} size={152} marginSize={0} title={t("auth.enroll.qr")} />
        </div>
        <ol className="space-y-2 text-sm">
          <li className="flex gap-2">
            <DeviceMobileIcon aria-hidden className="mt-0.5 size-5 shrink-0 text-primary" />
            {t("auth.enroll.openApp")}
          </li>
          <li className="flex gap-2">
            <ShieldCheckIcon aria-hidden className="mt-0.5 size-5 shrink-0 text-primary" />
            {t("auth.enroll.scanQr")}
          </li>
        </ol>
      </div>
      <details className="group rounded-box border border-border px-3 py-2 text-sm">
        <summary className="cursor-pointer py-1 font-medium">{t("auth.enroll.manual")}</summary>
        <div className="mt-2 flex items-center gap-2">
          <code aria-label={t("auth.enroll.key")} className="flex-1 font-mono text-sm break-all tabular-nums">
            {secret.match(/.{1,4}/g)?.join(" ")}
          </code>
          <CopyButton text={secret} label="auth.enroll.copyKey" />
        </div>
      </details>
      <form onSubmit={confirm} className="space-y-5">
        <Input
          label={t("auth.enroll.code")}
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
          {t("auth.enroll.enable")}
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
  const t = useAuthText();
  const router = useRouter();
  const [enrollment, setEnrollment] = useState<{ secret: string; otpauthUri: string }>();
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const start = async () => {
    setBusy(true);
    const { ok, data } = await postJson<{ secret: string; otpauthUri: string }>("/api/auth/mfa/enroll");
    setBusy(false);
    if (ok) setEnrollment(data);
    else setError(t.error(data.code));
  };
  if (enrollment)
    return (
      <section aria-labelledby="enroll-title" className="space-y-4">
        <h2 id="enroll-title" className="text-lg font-semibold">
          {t("auth.mfa.setup")}
        </h2>
        <MfaEnroll secret={enrollment.secret} otpauthUri={enrollment.otpauthUri} />
      </section>
    );
  return (
    <div className="space-y-6">
      <MfaVerifyForm onDone={() => router.replace("/")} />
      <div className="relative text-center text-xs text-muted before:absolute before:inset-x-0 before:top-1/2 before:border-t before:border-border">
        <span className="relative bg-surface px-3">{t("auth.mfa.firstTime")}</span>
      </div>
      <div className="space-y-2">
        <Button variant="secondary" loading={busy} className="w-full" onClick={start}>
          <DeviceMobileIcon aria-hidden />
          {t("auth.mfa.setup")}
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
