"use client";
/*
 * Lớp phủ (spec #448 §6): hộp thoại mở như nắp hộp lật từ cạnh trên, hộp thoại xác nhận, chú thích, menu thả xuống. Primitive Radix lo focus, Esc, khoá cuộn.
 */
import { XIcon } from "@phosphor-icons/react";
import { AlertDialog, Dialog as RDialog, DropdownMenu as RMenu, Tooltip as RTooltip } from "radix-ui";
import { useState, type ReactElement, type ReactNode } from "react";
import { Button, overlay } from "./components";
import { cx } from "./cx";

const panel =
  "chamfer fixed top-1/2 left-1/2 z-modal flex max-h-[calc(100dvh-2rem)] w-[calc(100vw-2rem)] max-w-md -translate-x-1/2 -translate-y-1/2 flex-col rounded-lg border border-border bg-surface-raised shadow-box-3 outline-none data-[state=closed]:animate-lid-close data-[state=open]:animate-lid-open";

/** Gốc hộp thoại (Radix Dialog): điều khiển `open`/`onOpenChange` hoặc để tự quản. */
export const Dialog = RDialog.Root;
/** Nút mở hộp thoại; dùng `asChild` để bọc {@link Button}. */
export const DialogTrigger = RDialog.Trigger;
/** Nút đóng hộp thoại tuỳ biến; dùng `asChild`. */
export const DialogClose = RDialog.Close;

/** Thuộc tính của {@link DialogContent}. */
export type DialogContentProps = {
  /** Tiêu đề (bắt buộc cho trình đọc màn hình). */
  title: ReactNode;
  /** Mô tả ngắn dưới tiêu đề. */
  description?: ReactNode;
  /** Nội dung (cuộn được khi dài). */
  children?: ReactNode;
  /** Hàng nút cuối hộp thoại, nút chính đặt cuối. */
  footer?: ReactNode;
};

/**
 * Nội dung hộp thoại giữa màn hình, mở bằng hiệu ứng nắp hộp lật xuống. Đóng bằng nút X, Esc hoặc chạm nền mờ.
 * Dùng cho thao tác ngắn cần tập trung; luồng dài hoặc chọn biến thể trên mobile dùng Sheet.
 *
 * @param props - {@link DialogContentProps}.
 * @returns Nền mờ + hộp thoại trong portal.
 */
export const DialogContent = ({ title, description, children, footer }: DialogContentProps) => {
  return (
    <RDialog.Portal>
      <RDialog.Overlay className={overlay} />
      <RDialog.Content className={panel} {...(description ? {} : { "aria-describedby": undefined })}>
        <div className="flex items-start justify-between gap-4 px-box pt-box pb-2">
          <div className="flex flex-col gap-1">
            <RDialog.Title className="font-display text-lg font-semibold">{title}</RDialog.Title>
            {description && <RDialog.Description className="text-sm text-muted">{description}</RDialog.Description>}
          </div>
          <RDialog.Close asChild>
            <Button variant="ghost" aria-label="Đóng" className="-mt-1 -mr-2 w-11 px-0">
              <XIcon aria-hidden />
            </Button>
          </RDialog.Close>
        </div>
        {children && <div className="flex flex-col gap-box overflow-y-auto px-box pb-box">{children}</div>}
        {footer && <div className="flex flex-col-reverse gap-2 border-t border-border px-box py-3 sm:flex-row sm:justify-end">{footer}</div>}
      </RDialog.Content>
    </RDialog.Portal>
  );
};

/** Thuộc tính của {@link ConfirmDialog}. */
export type ConfirmDialogProps = {
  /** Nút mở (một phần tử, thường là {@link Button}). */
  trigger: ReactElement;
  /** Câu hỏi xác nhận, nêu rõ đối tượng (vd. "Huỷ đơn MB-2041?"). */
  title: ReactNode;
  /** Hậu quả của thao tác. */
  description?: ReactNode;
  /** Nhãn nút xác nhận, là động từ của thao tác (vd. "Huỷ đơn"), không phải "OK". */
  confirmLabel: string;
  /** Nhãn nút quay lại; mặc định "Quay lại". */
  cancelLabel?: string;
  /** Thực hiện thao tác; trả Promise thì nút xác nhận hiện đang xử lý tới khi xong. */
  onConfirm: () => void | Promise<void>;
  /** `danger` cho thao tác phá huỷ; mặc định `default`. */
  tone?: "default" | "danger";
};

/**
 * Hộp thoại xác nhận trước thao tác khó hoàn tác (huỷ đơn, xoá). Không đóng khi chạm nền mờ; focus mặc định ở nút quay lại.
 *
 * @param props - {@link ConfirmDialogProps}.
 * @returns Trigger + hộp thoại xác nhận.
 */
export const ConfirmDialog = ({ trigger, title, description, confirmLabel, cancelLabel = "Quay lại", onConfirm, tone = "default" }: ConfirmDialogProps) => {
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const confirm = async () => {
    setBusy(true);
    try {
      await onConfirm();
      setOpen(false);
    } finally {
      setBusy(false);
    }
  };
  return (
    <AlertDialog.Root open={open} onOpenChange={setOpen}>
      <AlertDialog.Trigger asChild>{trigger}</AlertDialog.Trigger>
      <AlertDialog.Portal>
        <AlertDialog.Overlay className={overlay} />
        <AlertDialog.Content className={panel} {...(description ? {} : { "aria-describedby": undefined })}>
          <div className="flex flex-col gap-1 px-box pt-box pb-4">
            <AlertDialog.Title className="font-display text-lg font-semibold">{title}</AlertDialog.Title>
            {description && <AlertDialog.Description className="text-sm text-muted">{description}</AlertDialog.Description>}
          </div>
          <div className="flex flex-col-reverse gap-2 border-t border-border px-box py-3 sm:flex-row sm:justify-end">
            <AlertDialog.Cancel asChild>
              <Button variant="secondary">{cancelLabel}</Button>
            </AlertDialog.Cancel>
            <Button data-confirm variant={tone === "danger" ? "danger" : "primary"} loading={busy} onClick={confirm}>
              {confirmLabel}
            </Button>
          </div>
        </AlertDialog.Content>
      </AlertDialog.Portal>
    </AlertDialog.Root>
  );
};

/** Thuộc tính của {@link Tooltip}. */
export type TooltipProps = {
  /** Nội dung chú thích, ngắn; không đặt thông tin bắt buộc ở đây (cảm ứng không rê chuột được). */
  content: ReactNode;
  /** Phần tử được chú thích (một phần tử focus được). */
  children: ReactElement;
};

/**
 * Chú thích nhỏ khi rê chuột hoặc focus bàn phím, trễ 300ms. Chỉ bổ sung, không thay nhãn của nút icon (`aria-label`).
 *
 * @param props - {@link TooltipProps}.
 * @returns Phần tử con có chú thích.
 */
export const Tooltip = ({ content, children }: TooltipProps) => {
  return (
    <RTooltip.Provider delayDuration={300}>
      <RTooltip.Root>
        <RTooltip.Trigger asChild>{children}</RTooltip.Trigger>
        <RTooltip.Portal>
          <RTooltip.Content
            sideOffset={6}
            collisionPadding={8}
            className="z-toast max-w-64 rounded-box bg-foreground px-2.5 py-1.5 text-xs font-medium text-background shadow-box-2 data-[state=closed]:animate-fade-out data-[state=delayed-open]:animate-fade-in"
          >
            {content}
          </RTooltip.Content>
        </RTooltip.Portal>
      </RTooltip.Root>
    </RTooltip.Provider>
  );
};

/** Một mục của {@link DropdownMenu}. */
export type MenuItem = {
  /** Nhãn (động từ: "In phiếu", "Huỷ đơn"). */
  label: string;
  /** Icon trước nhãn (trang trí). */
  icon?: ReactNode;
  /** Gọi khi chọn. */
  onSelect: () => void;
  /** `danger` cho thao tác phá huỷ: tách khỏi mục thường bằng đường kẻ, chữ đỏ. */
  tone?: "danger";
  /** Khoá mục. */
  disabled?: boolean;
};

/** Thuộc tính của {@link DropdownMenu}. */
export type DropdownMenuProps = {
  /** Nút mở (một phần tử, thường là {@link Button}). */
  trigger: ReactElement;
  /** Nhãn của menu cho trình đọc màn hình. */
  label: string;
  /** Các mục; mục `danger` tự xếp cuối. */
  items: MenuItem[];
};

/**
 * Menu thả xuống cho thao tác phụ (khi không đủ chỗ cho nút). Điều khiển bằng bàn phím đầy đủ (Radix).
 *
 * @param props - {@link DropdownMenuProps}.
 * @returns Trigger + menu trong portal.
 */
export const DropdownMenu = ({ trigger, label, items }: DropdownMenuProps) => {
  const normal = items.filter((i) => i.tone !== "danger");
  const danger = items.filter((i) => i.tone === "danger");
  const item = (i: MenuItem) => (
    <RMenu.Item
      key={i.label}
      disabled={i.disabled}
      onSelect={i.onSelect}
      className={cx(
        "flex h-10 cursor-pointer items-center gap-2.5 rounded-box px-3 text-sm outline-none select-none data-disabled:pointer-events-none data-disabled:opacity-50 data-highlighted:bg-surface-sunken [&_svg]:size-4",
        i.tone === "danger" && "text-danger data-highlighted:bg-danger-soft",
      )}
    >
      {i.icon && <span aria-hidden>{i.icon}</span>}
      {i.label}
    </RMenu.Item>
  );
  return (
    <RMenu.Root>
      <RMenu.Trigger asChild>{trigger}</RMenu.Trigger>
      <RMenu.Portal>
        <RMenu.Content
          aria-label={label}
          align="end"
          sideOffset={6}
          collisionPadding={8}
          className="z-modal min-w-48 origin-top rounded-lg border border-border bg-surface-raised p-1 shadow-box-3 data-[state=open]:animate-lid-open"
        >
          {normal.map(item)}
          {danger.length > 0 && normal.length > 0 && <RMenu.Separator className="my-1 h-px bg-border" />}
          {danger.map(item)}
        </RMenu.Content>
      </RMenu.Portal>
    </RMenu.Root>
  );
};
