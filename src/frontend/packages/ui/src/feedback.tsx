"use client";
/*
 * Phản hồi (spec #448 §6): nhãn trạng thái, thông báo trong trang, thông báo nổi (toast), khung chờ, trạng thái rỗng.
 * Màu trạng thái luôn đi kèm icon/chữ, không chỉ dựa vào màu.
 */
import { CheckCircleIcon, InfoIcon, WarningCircleIcon, WarningIcon, XIcon } from "@phosphor-icons/react";
import { Toast } from "radix-ui";
import { useSyncExternalStore, type ReactNode } from "react";
import { cx } from "./cx";

const tones = {
  neutral: { bg: "bg-surface-sunken border-border", text: "text-foreground" },
  primary: { bg: "bg-accent border-transparent", text: "text-accent-foreground" },
  success: { bg: "bg-success-soft border-transparent", text: "text-success" },
  warning: { bg: "bg-warning-soft border-transparent", text: "text-warning" },
  danger: { bg: "bg-danger-soft border-transparent", text: "text-danger" },
  info: { bg: "bg-info-soft border-transparent", text: "text-info" },
};

/** Sắc thái của nhãn/thông báo. */
export type Tone = keyof typeof tones;

/** Thuộc tính của {@link Badge}. */
export type BadgeProps = {
  /** Sắc thái; mặc định `neutral`. */
  tone?: Tone;
  /** Icon trước chữ (trang trí). */
  icon?: ReactNode;
  /** Nội dung nhãn, ngắn (1-3 từ). */
  children: ReactNode;
  /** Class bổ sung. */
  className?: string;
};

/**
 * Nhãn trạng thái nhỏ (tồn kho, trạng thái đơn, vật liệu) có góc vát như tem dán.
 *
 * @param props - {@link BadgeProps}.
 * @returns Phần tử nhãn.
 */
export const Badge = ({ tone = "neutral", icon, children, className }: BadgeProps) => {
  return (
    <span className={cx("chamfer-sm inline-flex h-6 items-center gap-1 rounded-sm border px-2 text-xs font-medium whitespace-nowrap [&_svg]:size-3.5", tones[tone].bg, tones[tone].text, className)}>
      {icon && <span aria-hidden>{icon}</span>}
      {children}
    </span>
  );
};

const alertIcons = { info: InfoIcon, success: CheckCircleIcon, warning: WarningIcon, danger: WarningCircleIcon };

/** Sắc thái của {@link Alert}. */
export type AlertTone = keyof typeof alertIcons;

/** Thuộc tính của {@link Alert}. */
export type AlertProps = {
  /** Sắc thái; mặc định `info`. `danger`/`warning` được đọc ngay (role alert). */
  tone?: AlertTone;
  /** Câu chính: chuyện gì xảy ra. */
  title: ReactNode;
  /** Chi tiết và cách xử lý. */
  children?: ReactNode;
  /** Hành động khắc phục (vd. nút Thử lại). */
  action?: ReactNode;
};

/**
 * Thông báo trong trang, đặt gần nơi liên quan; nói rõ nguyên nhân và cách xử lý.
 *
 * @param props - {@link AlertProps}.
 * @returns Khối thông báo.
 */
export const Alert = ({ tone = "info", title, children, action }: AlertProps) => {
  const Icon = alertIcons[tone];
  return (
    <div
      role={tone === "danger" || tone === "warning" ? "alert" : "status"}
      className={cx("chamfer flex gap-3 rounded-lg border p-4 text-sm text-foreground", tones[tone].bg)}
    >
      <Icon aria-hidden weight="fill" className={cx("mt-0.5 size-5 shrink-0", tones[tone].text)} />
      <div className="flex flex-1 flex-col gap-1">
        <p className="font-semibold">{title}</p>
        {children && <div className="text-muted">{children}</div>}
        {action && <div className="mt-2">{action}</div>}
      </div>
    </div>
  );
};

/** Nội dung một thông báo nổi. */
export type ToastOptions = {
  /** Câu chính, ngắn. */
  title: string;
  /** Chi tiết một dòng. */
  description?: string;
  /** Sắc thái icon; mặc định `success`. */
  tone?: AlertTone;
  /** Hành động kèm theo (vd. Hoàn tác). */
  action?: { label: string; onClick: () => void };
};

type Item = ToastOptions & { id: number };
let items: Item[] = [];
let seq = 0;
const listeners = new Set<() => void>();
const emit = () => listeners.forEach((l) => l());
const subscribe = (l: () => void) => {
  listeners.add(l);
  return () => listeners.delete(l);
};
const remove = (id: number) => {
  items = items.filter((t) => t.id !== id);
  emit();
};

/**
 * Hiện thông báo nổi ngắn (tự ẩn sau 4 giây, không lấy focus, trình đọc màn hình đọc lịch sự). Cần {@link Toaster} trong layout.
 *
 * @param options - {@link ToastOptions}.
 * @returns Mã thông báo.
 */
export const toast = (options: ToastOptions) => {
  const id = ++seq;
  items = [...items, { ...options, id }];
  emit();
  return id;
};

/**
 * Vùng hiển thị thông báo nổi: góc dưới phải (trên điều hướng dưới ở mobile). Đặt một lần trong layout.
 *
 * @returns Provider + viewport của toast.
 */
export const Toaster = () => {
  const list = useSyncExternalStore(subscribe, () => items, () => items);
  return (
    <Toast.Provider duration={4000} label="Thông báo">
      {list.map((t) => {
        const Icon = alertIcons[t.tone ?? "success"];
        return (
          <Toast.Root
            key={t.id}
            onOpenChange={(open) => !open && remove(t.id)}
            className="chamfer flex items-start gap-3 rounded-lg border border-border bg-surface-raised p-4 shadow-box-3 data-[state=closed]:animate-slot-out data-[state=open]:animate-slot-in data-[swipe=move]:translate-x-(--radix-toast-swipe-move-x) data-[swipe=end]:animate-slot-out"
          >
            <Icon aria-hidden weight="fill" className={cx("mt-0.5 size-5 shrink-0", tones[t.tone ?? "success"].text)} />
            <div className="flex flex-1 flex-col gap-0.5 text-sm">
              <Toast.Title className="font-semibold">{t.title}</Toast.Title>
              {t.description && <Toast.Description className="text-muted">{t.description}</Toast.Description>}
            </div>
            {t.action && (
              <Toast.Action altText={t.action.label} asChild>
                <button type="button" onClick={t.action.onClick} className="h-8 rounded-box px-2 text-sm font-semibold text-primary hover:bg-accent">
                  {t.action.label}
                </button>
              </Toast.Action>
            )}
            <Toast.Close aria-label="Đóng thông báo" className="-m-1 flex size-8 items-center justify-center rounded-box text-muted hover:bg-surface-sunken hover:text-foreground">
              <XIcon aria-hidden className="size-4" />
            </Toast.Close>
          </Toast.Root>
        );
      })}
      <Toast.Viewport className="fixed right-4 bottom-[calc(5rem+env(safe-area-inset-bottom))] left-4 z-toast flex flex-col gap-2 outline-none sm:bottom-4 sm:left-auto sm:w-96" />
    </Toast.Provider>
  );
};

/**
 * Khung chờ có ánh sáng lướt qua, đúng hình nội dung sắp hiện (thay cho vòng quay khi tải lâu hơn 1 giây).
 *
 * @param props - `className` đặt kích thước/hình (vd. `h-4 w-32`, `aspect-square`).
 * @returns Khối trang trí ẩn với trình đọc màn hình.
 */
export const Skeleton = ({ className }: { className?: string }) => {
  return (
    <div
      aria-hidden="true"
      className={cx(
        "animate-shimmer rounded-box bg-[linear-gradient(90deg,var(--color-surface-sunken)_30%,var(--color-surface)_50%,var(--color-surface-sunken)_70%)] bg-size-[200%_100%]",
        className,
      )}
    />
  );
};

/** Thuộc tính của {@link EmptyState}. */
export type EmptyStateProps = {
  /** Câu chính: chưa có gì. */
  title: ReactNode;
  /** Gợi ý cách thêm nội dung. */
  description?: ReactNode;
  /** Hành động chính (một nút). */
  action?: ReactNode;
};

/**
 * Trạng thái rỗng: hộp isometric mở nắp (nắp bật lên một lần khi hiện) + câu chính + gợi ý + một hành động.
 *
 * @param props - {@link EmptyStateProps}.
 * @returns Khối trạng thái rỗng.
 */
export const EmptyState = ({ title, description, action }: EmptyStateProps) => {
  return (
    <div className="chamfer flex flex-col items-center gap-3 rounded-lg border border-dashed border-border-strong px-box py-10 text-center">
      <svg aria-hidden viewBox="0 -4 32 38" className="mb-1 size-16 overflow-visible text-muted">
        <ellipse cx="16" cy="32" rx="12" ry="2.5" className="fill-[color-mix(in_oklab,var(--shade)_14%,transparent)]" />
        <path d="M4 13 16 20 28 13 28 25 16 32 4 25Z" className="fill-surface-sunken stroke-current" strokeWidth={1.5} strokeLinejoin="round" />
        <path d="M16 20V32" className="stroke-current" strokeWidth={1.5} />
        <g className="origin-[16px_13px] animate-lid-open stroke-current" strokeWidth={1.5} strokeLinejoin="round">
          <path d="M4 13 0 5 12 -2 16 6Z" className="fill-accent" />
          <path d="M28 13 32 5 20 -2 16 6Z" className="fill-accent" />
        </g>
      </svg>
      <h2 className="font-display text-base font-semibold">{title}</h2>
      {description && <p className="max-w-prose text-sm text-muted">{description}</p>}
      {action && <div className="mt-1">{action}</div>}
    </div>
  );
};
