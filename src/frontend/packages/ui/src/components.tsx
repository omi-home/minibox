"use client";
/*
 * Component cơ bản khối hộp (spec §6.4) trên primitive Radix + token theme.css. Ngưỡng: mobile mặc định, sm: ≥ 640 (tablet trở lên).
 * Vùng chạm tối thiểu 44px (h-11). Safe area dùng env(safe-area-inset-*) — layout app phải bật viewport-fit=cover.
 * Bo góc: control rounded-box, khối chứa rounded-lg (xem theme.css).
 */
import { CircleNotchIcon, XIcon } from "@phosphor-icons/react";
import { Dialog, Slot } from "radix-ui";
import { type ComponentProps, type ElementType, type ReactNode } from "react";

const cx = (...c: (string | false | undefined)[]) => c.filter(Boolean).join(" ");

// Nút nổi (primary/secondary/danger) là phím có cạnh đáy 3px; nhấn thì lún 3px và mất cạnh. ghost phẳng.
const key = "shadow-key active:translate-y-[3px] active:shadow-key-pressed";
const buttonVariants = {
  primary: `${key} bg-primary text-primary-foreground hover:bg-primary-hover [--key-edge:var(--color-primary-edge)]`,
  secondary: `${key} border border-border-strong bg-surface text-foreground hover:bg-surface-sunken`,
  ghost: "text-foreground hover:bg-surface-sunken active:translate-y-px",
  danger: `${key} bg-danger text-white hover:bg-danger-hover [--key-edge:var(--color-danger-edge)]`,
};

/** Thuộc tính của {@link Button}. */
export type ButtonProps = ComponentProps<"button"> & {
  /** Kiểu nút; mặc định `primary`. Mỗi màn hình chỉ một nút `primary`. */
  variant?: keyof typeof buttonVariants;
  /** Đang xử lý: khoá nút, hiện vòng quay, `aria-busy`. */
  loading?: boolean;
  /** Truyền style nút cho phần tử con duy nhất (vd. link) thay vì render `<button>`. */
  asChild?: boolean;
};

/**
 * Nút khối hộp: cao 44px, nổi như phím bấm có cạnh dày, nhấn lún xuống; khoá khi `loading`/`disabled`.
 *
 * @param props - {@link ButtonProps}.
 * @returns Phần tử nút.
 */
export const Button = ({ variant = "primary", loading, asChild, className, type, disabled, children, ...props }: ButtonProps) => {
  const cls = cx(
    "inline-flex h-11 select-none items-center justify-center gap-2 whitespace-nowrap rounded-box px-4 text-sm font-semibold",
    "transition-[background-color,box-shadow,translate] duration-100 ease-out",
    "disabled:pointer-events-none disabled:opacity-50 [&_svg]:size-5 [&_svg]:shrink-0",
    buttonVariants[variant],
    className,
  );
  if (asChild)
    return (
      <Slot.Root className={cls} {...props}>
        {children}
      </Slot.Root>
    );
  return (
    <button type={type ?? "button"} disabled={disabled || loading} aria-busy={loading || undefined} className={cls} {...props}>
      {loading && <CircleNotchIcon aria-hidden className="animate-spin" />}
      {children}
    </button>
  );
}

const depths = { 1: "shadow-box-1", 2: "shadow-box-2", 3: "shadow-box-3" };

/** Thuộc tính của {@link Box}. */
export type BoxProps = ComponentProps<"div"> & {
  /** Tầng độ sâu (bóng); mặc định 1. */
  depth?: keyof typeof depths;
  /** Thẻ bấm được (sản phẩm, đơn): rê chuột thì nổi lên và nghiêng nhẹ về sau như nhấc hộp. */
  interactive?: boolean;
  /** Truyền style hộp cho phần tử con duy nhất (vd. `<section>`, `<article>`). */
  asChild?: boolean;
};

const lift =
  "transition-[translate,transform,box-shadow] duration-300 ease-out-box hover:-translate-y-1 hover:shadow-box-2 hover:transform-[perspective(60rem)_rotateX(3deg)]";

/**
 * Thẻ hộp: nền surface, viền mảnh, cạnh đáy dày, bo góc khối chứa, bóng theo tầng.
 *
 * @param props - {@link BoxProps}.
 * @returns Phần tử hộp.
 */
export const Box = ({ depth = 1, interactive, asChild, className, ...props }: BoxProps) => {
  const Comp = asChild ? Slot.Root : "div";
  return (
    <Comp className={cx("rounded-lg border border-border bg-surface p-box", depths[depth], interactive && lift, className)} {...props} />
  );
}

/** Gốc sheet (Radix Dialog): điều khiển `open`/`onOpenChange` hoặc để tự quản. */
export const Sheet = Dialog.Root;
/** Nút mở sheet; dùng `asChild` để bọc {@link Button}. */
export const SheetTrigger = Dialog.Trigger;
/** Nút đóng sheet tuỳ biến; dùng `asChild` để bọc {@link Button}. */
export const SheetClose = Dialog.Close;

/** Thuộc tính của {@link SheetContent}. */
export type SheetContentProps = {
  /** Tiêu đề sheet (bắt buộc cho trình đọc màn hình). */
  title: ReactNode;
  /** Mô tả ngắn dưới tiêu đề. */
  description?: ReactNode;
  /** Nội dung sheet (cuộn được). */
  children?: ReactNode;
  /** Thanh hành động cố định ở đáy sheet (vd. nút Áp dụng). */
  footer?: ReactNode;
};

/**
 * Nội dung sheet: trượt từ dưới lên trên mobile (chừa safe area), panel bên phải từ tablet trở lên.
 * Dùng cho biến thể sản phẩm, bộ lọc (BRD §5.2). Đóng bằng nút X, Esc hoặc chạm nền mờ.
 *
 * @param props - {@link SheetContentProps}.
 * @returns Overlay + panel trong portal.
 */
export const SheetContent = ({ title, description, children, footer }: SheetContentProps) => {
  return (
    <Dialog.Portal>
      <Dialog.Overlay className="fixed inset-0 z-40 bg-black/40 backdrop-blur-[2px] data-[state=closed]:animate-fade-out data-[state=open]:animate-fade-in" />
      <Dialog.Content
        className={cx(
          "fixed inset-x-0 bottom-0 z-50 flex max-h-[88dvh] flex-col rounded-t-lg border border-border bg-surface shadow-box-3 outline-none",
          "data-[state=closed]:animate-sheet-down data-[state=open]:animate-sheet-up",
          "sm:inset-y-0 sm:right-0 sm:left-auto sm:max-h-none sm:w-[26rem] sm:rounded-none sm:rounded-l-lg",
          "sm:data-[state=closed]:animate-sheet-right sm:data-[state=open]:animate-sheet-left",
        )}
        // Không có mô tả: tắt aria-describedby để Radix không cảnh báo thiếu Description.
        {...(description ? {} : { "aria-describedby": undefined })}
      >
        <div className="flex items-start justify-between gap-4 border-b border-border px-box pt-box pb-3">
          <div className="flex flex-col gap-0.5">
            <Dialog.Title className="text-lg font-semibold">{title}</Dialog.Title>
            {description && <Dialog.Description className="text-sm text-muted">{description}</Dialog.Description>}
          </div>
          <Dialog.Close asChild>
            <Button variant="ghost" aria-label="Đóng" className="-mt-1 -mr-2 w-11 px-0">
              <XIcon aria-hidden />
            </Button>
          </Dialog.Close>
        </div>
        <div className="flex flex-1 flex-col gap-box overflow-y-auto p-box">{children}</div>
        {footer && (
          <div className="flex gap-2 border-t border-border px-box pt-3 pb-[calc(0.75rem+env(safe-area-inset-bottom))] sm:pb-3 [&>*]:flex-1">
            {footer}
          </div>
        )}
      </Dialog.Content>
    </Dialog.Portal>
  );
}

/** Một cột của {@link DataTable}. */
export type Column<T> = {
  /** Tiêu đề cột (cũng là nhãn trong chế độ thẻ). */
  header: string;
  /** Nội dung ô cho một dòng. */
  cell: (row: T) => ReactNode;
  /** Cột số (số lượng, tiền): canh phải, chữ số đều độ rộng. */
  numeric?: boolean;
};

/** Thuộc tính của {@link DataTable}. */
export type DataTableProps<T> = {
  /** Mô tả bảng (caption, trình đọc màn hình đọc được). */
  caption: string;
  /** Danh sách cột; cột đầu là tiêu đề thẻ trên mobile. */
  columns: Column<T>[];
  /** Dữ liệu. */
  rows: T[];
  /** Khoá ổn định của một dòng. */
  rowKey: (row: T) => string;
  /** Nội dung khi không có dòng nào; mặc định "Chưa có dữ liệu". */
  empty?: ReactNode;
};

/**
 * Bảng ↔ thẻ: bảng từ tablet trở lên, danh sách thẻ hộp trên mobile (spec §6.4). Đổi bằng CSS (`display: none`), không JS.
 *
 * @param props - {@link DataTableProps}.
 * @returns Bảng và danh sách thẻ, hoặc trạng thái rỗng.
 */
export const DataTable = <T,>({ caption, columns, rows, rowKey, empty = "Chưa có dữ liệu" }: DataTableProps<T>) => {
  if (rows.length === 0)
    return (
      <div role="status" className="rounded-lg border border-dashed border-border-strong px-box py-10 text-center text-sm text-muted">
        {empty}
      </div>
    );
  const [first, ...rest] = columns;
  const num = (c: Column<T>) => (c.numeric ? "text-right tabular-nums" : "text-left");
  return (
    <div>
      <div className="hidden overflow-hidden rounded-lg border border-border bg-surface shadow-box-1 sm:block">
        <table className="w-full border-collapse text-sm">
          <caption className="sr-only">{caption}</caption>
          <thead className="bg-surface-sunken text-muted">
            <tr>
              {columns.map((c) => (
                <th key={c.header} scope="col" className={cx("px-4 py-3 font-medium", num(c))}>
                  {c.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {rows.map((r) => (
              <tr key={rowKey(r)} className="transition-colors hover:bg-surface-sunken/60">
                {columns.map((c, i) => (
                  <td key={c.header} className={cx("px-4 py-3", num(c), i === 0 && "font-medium")}>
                    {c.cell(r)}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <ul aria-label={caption} className="flex flex-col gap-3 sm:hidden">
        {rows.map((r) => (
          <li key={rowKey(r)}>
            <Box>
              {first && <div className="mb-3 font-semibold">{first.cell(r)}</div>}
              <dl className="grid grid-cols-2 gap-x-4 gap-y-2 text-sm">
                {rest.map((c) => (
                  <div key={c.header} className="flex flex-col">
                    <dt className="text-xs text-muted">{c.header}</dt>
                    <dd className={cx("font-medium", c.numeric && "tabular-nums")}>{c.cell(r)}</dd>
                  </div>
                ))}
              </dl>
            </Box>
          </li>
        ))}
      </ul>
    </div>
  );
};

/** Một mục của {@link BottomNav}. */
export type BottomNavItem = {
  /** Đường dẫn. */
  href: string;
  /** Nhãn hiển thị (luôn hiện cạnh icon). */
  label: string;
  /** Biểu tượng (trang trí, ẩn với trình đọc màn hình). */
  icon?: ReactNode;
  /** Mục của trang hiện tại (`aria-current="page"`). */
  current?: boolean;
};

/** Thuộc tính của {@link BottomNav}. */
export type BottomNavProps = {
  /** Nhãn vùng điều hướng. */
  label: string;
  /** Các mục cấp cao nhất (≤ 5). */
  items: BottomNavItem[];
  /** Component link (vd. `next/link`); mặc định `<a>`. */
  link?: ElementType;
};

/**
 * Điều hướng dưới cho mobile: cố định đáy màn hình, chừa safe area, ẩn từ tablet trở lên.
 * Mục hiện tại có nền nhấn sau icon. Kèm khoảng đệm cùng chiều cao để nội dung cuối trang không bị che.
 *
 * @param props - {@link BottomNavProps}.
 * @returns Khoảng đệm + thanh điều hướng.
 */
export const BottomNav = ({ label, items, link: Link = "a" }: BottomNavProps) => {
  return (
    <>
      <div aria-hidden className="h-[calc(4.25rem+env(safe-area-inset-bottom))] sm:hidden" />
      <nav
        aria-label={label}
        className="fixed inset-x-0 bottom-0 z-30 border-t border-border bg-surface/90 pb-[env(safe-area-inset-bottom)] backdrop-blur-md sm:hidden"
      >
        <ul className="mx-auto flex h-[4.25rem] max-w-md">
          {items.map((i) => (
            <li key={i.href} className="flex-1">
              <Link
                href={i.href}
                aria-current={i.current ? "page" : undefined}
                className="group flex h-full flex-col items-center justify-center gap-1 text-xs font-medium text-muted transition-colors aria-[current=page]:text-foreground"
              >
                {i.icon && (
                  <span
                    aria-hidden
                    className="flex h-8 w-14 items-center justify-center rounded-box transition-colors group-aria-[current=page]:bg-accent group-aria-[current=page]:text-accent-foreground [&_svg]:size-6"
                  >
                    {i.icon}
                  </span>
                )}
                {i.label}
              </Link>
            </li>
          ))}
        </ul>
      </nav>
    </>
  );
}
