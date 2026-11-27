/*
 * Khung field dùng chung nội bộ cho form.tsx và date.tsx (không export ra index): nhãn, gợi ý, lỗi, bề mặt ô nhập, trigger + popover.
 */
import { WarningCircleIcon } from "@phosphor-icons/react";
import { Popover } from "radix-ui";
import { useId, useState, type ReactNode } from "react";

/** Ghép class, bỏ giá trị rỗng. */
export const cx = (...c: (string | false | undefined)[]) => c.filter(Boolean).join(" ");

/** Phần chung của mọi control có nhãn. */
export type FieldBase = {
  /** Nhãn hiển thị. */
  label: ReactNode;
  /** Gợi ý dưới control. */
  hint?: ReactNode;
  /** Thông báo lỗi (nói rõ nguyên nhân và cách sửa); có giá trị thì control `aria-invalid`. */
  error?: ReactNode;
};

/** Thuộc tính a11y mà Field truyền cho control. */
export type A11y = { id: string; "aria-invalid"?: true; "aria-describedby"?: string };

/**
 * Khung nhãn + control + gợi ý + lỗi, sinh id và nối aria-describedby.
 *
 * @param props - Nhãn/gợi ý/lỗi; `id` tuỳ chọn; `required` thêm dấu `*`; `children` nhận thuộc tính a11y và trả control.
 * @returns Nhóm field.
 */
export const Field = ({ label, hint, error, id, required, children }: FieldBase & { id?: string; required?: boolean; children: (a: A11y) => ReactNode }) => {
  const auto = useId();
  const fid = id ?? auto;
  const hintId = hint ? `${fid}-hint` : undefined;
  const errorId = error ? `${fid}-error` : undefined;
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={fid} className="text-sm font-medium">
        {label}
        {required && (
          <span aria-hidden className="ml-0.5 text-danger">
            *
          </span>
        )}
      </label>
      {children({ id: fid, "aria-invalid": error ? true : undefined, "aria-describedby": cx(hintId, errorId) || undefined })}
      <Messages hintId={hintId} errorId={errorId} hint={hint} error={error} />
    </div>
  );
}

/**
 * Dòng gợi ý và lỗi dưới control.
 *
 * @param props - Nội dung và id của gợi ý/lỗi.
 * @returns Các đoạn văn bản, hoặc không gì.
 */
export const Messages = ({ hint, error, hintId, errorId }: { hint?: ReactNode; error?: ReactNode; hintId?: string; errorId?: string }) => {
  return (
    <>
      {hint && (
        <p id={hintId} className="text-sm text-muted">
          {hint}
        </p>
      )}
      {error && (
        <p id={errorId} className="flex items-center gap-1.5 text-sm font-medium text-danger">
          <WarningCircleIcon aria-hidden className="size-4 shrink-0" weight="fill" />
          {error}
        </p>
      )}
    </>
  );
}

/*
 * Trạng thái phải phân biệt rõ: dùng được = nền trắng (surface) + viền đậm + bóng lõm nhẹ;
 * bị khoá = nền xám (surface-sunken), chữ nhạt, viền nhạt, không bóng. Không dùng nền xám cho ô đang dùng được.
 */
/** Trạng thái khoá của ô nhập. */
export const disabledField = "disabled:cursor-not-allowed disabled:border-border disabled:bg-surface-sunken disabled:text-muted disabled:shadow-none";
/** Bề mặt ô nhập dùng chung cho input/textarea/select và trigger của bộ chọn ngày. */
export const surface = cx(
  "w-full rounded-box border border-border-strong bg-surface px-3 text-base text-foreground shadow-inset",
  "transition-[border-color,box-shadow] duration-150 placeholder:text-muted",
  "hover:border-muted focus-visible:border-primary focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-focus/25",
  "aria-invalid:border-danger aria-invalid:focus-visible:ring-danger/20",
  disabledField,
);

/**
 * Giá trị có thể điều khiển hoặc tự quản: `value !== undefined` là điều khiển (dùng `null` cho rỗng).
 *
 * @param value - Giá trị điều khiển.
 * @param initial - Giá trị ban đầu khi tự quản.
 * @param onChange - Gọi khi đổi.
 * @returns Cặp [giá trị hiện tại, hàm đặt].
 */
export function useControllable<T>(value: T | null | undefined, initial: T | null | undefined, onChange?: (v: T | null) => void) {
  const [inner, setInner] = useState<T | null>(initial ?? null);
  const controlled = value !== undefined;
  const set = (v: T | null) => {
    if (!controlled) setInner(v);
    onChange?.(v);
  };
  return [controlled ? value : inner, set] as const;
}

/**
 * Nút mở popover trông như ô nhập: chữ đã định dạng hoặc placeholder, icon bên phải.
 *
 * @param props - Thuộc tính a11y của Field, nội dung hiển thị, icon, trạng thái khoá.
 * @returns Nút trigger.
 */
export const Trigger = ({ a, text, placeholder, icon, disabled }: { a: object; text?: string; placeholder: string; icon: ReactNode; disabled?: boolean }) => {
  return (
    <Popover.Trigger asChild>
      <button
        {...a}
        type="button"
        disabled={disabled}
        className={cx(surface, "flex h-11 items-center gap-2 text-left data-[state=open]:border-primary data-[state=open]:ring-4 data-[state=open]:ring-focus/25", !text && "text-muted")}
      >
        <span className="flex-1 truncate tabular-nums">{text ?? placeholder}</span>
        <span aria-hidden className="text-muted [&_svg]:size-5">
          {icon}
        </span>
      </button>
    </Popover.Trigger>
  );
}

/**
 * Khung popover dưới trigger, tự lật/dịch khi sát mép màn hình.
 *
 * @param props - `children` là nội dung; `label` là nhãn cho trình đọc màn hình; `matchWidth` cho rộng bằng trigger.
 * @returns Nội dung popover trong portal.
 */
export const Panel = ({ children, label, matchWidth }: { children: ReactNode; label: string; matchWidth?: boolean }) => {
  return (
    <Popover.Portal>
      <Popover.Content
        aria-label={label}
        align="start"
        sideOffset={6}
        collisionPadding={16}
        className={cx("z-50 max-w-[calc(100vw-2rem)] rounded-lg border border-border bg-surface shadow-box-3 outline-none data-[state=open]:animate-fade-in", matchWidth && "w-[var(--radix-popover-trigger-width)] min-w-64")}
      >
        {children}
      </Popover.Content>
    </Popover.Portal>
  );
}
