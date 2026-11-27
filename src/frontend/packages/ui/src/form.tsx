"use client";
/*
 * Control form khối hộp. Ô nhập lõm (shadow-inset), đối lập với nút nổi. Mọi control dùng chung khung Field: nhãn ở trên,
 * gợi ý và lỗi ở dưới gắn bằng aria-describedby, lỗi có icon (không chỉ dựa vào màu), trường bắt buộc có dấu `*`.
 * Select là control NATIVE (danh sách ngắn, mobile hiện bảng chọn của hệ điều hành); danh sách dài cần tìm kiếm dùng Combobox.
 */
import { CaretUpDownIcon, CheckIcon, MagnifyingGlassIcon, MinusIcon, PlusIcon, XIcon } from "@phosphor-icons/react";
import { Command } from "cmdk";
import { Checkbox as RCheckbox, Popover, RadioGroup as RRadioGroup, Switch as RSwitch } from "radix-ui";
import { useId, useState, type ComponentProps, type ReactNode } from "react";
import { cx, Field, Messages, Panel, surface, Trigger, useControllable, type FieldBase } from "./field";

/** Thuộc tính của {@link Input}. */
export type InputProps = ComponentProps<"input"> & FieldBase;

/**
 * Ô nhập một dòng. `type` native quyết định bàn phím mobile và bộ chọn: `email`, `tel`, `password`,
 * `date`/`time`/`datetime-local` (bộ chọn ngày giờ của hệ điều hành, giá trị ISO).
 *
 * @param props - {@link InputProps}.
 * @returns Field có ô nhập.
 */
export const Input = ({ label, hint, error, id, required, className, ...props }: InputProps) => {
  return (
    <Field label={label} hint={hint} error={error} id={id} required={required}>
      {(a) => (
        <input
          {...a}
          required={required}
          className={cx(
            surface,
            "h-(--control-h) [&::-webkit-calendar-picker-indicator]:cursor-pointer [&::-webkit-calendar-picker-indicator]:opacity-60",
            className,
          )}
          {...props}
        />
      )}
    </Field>
  );
}

/** Thuộc tính của {@link Textarea}. */
export type TextareaProps = ComponentProps<"textarea"> & FieldBase;

/**
 * Ô nhập nhiều dòng (ghi chú đơn, địa chỉ chi tiết), kéo giãn theo chiều dọc.
 *
 * @param props - {@link TextareaProps}.
 * @returns Field có textarea.
 */
export const Textarea = ({ label, hint, error, id, required, className, rows = 3, ...props }: TextareaProps) => {
  return (
    <Field label={label} hint={hint} error={error} id={id} required={required}>
      {(a) => <textarea {...a} rows={rows} required={required} className={cx(surface, "min-h-24 resize-y py-2.5", className)} {...props} />}
    </Field>
  );
}

/** Thuộc tính của {@link Select}. */
export type SelectProps = ComponentProps<"select"> &
  FieldBase & {
    /** Dòng gợi ý khi chưa chọn (option rỗng, không chọn lại được). */
    placeholder?: string;
  };

/**
 * Hộp chọn native: danh sách mở bằng bảng chọn của hệ điều hành (bánh xe trên iOS), các `<option>`/`<optgroup>` truyền qua `children`.
 *
 * @param props - {@link SelectProps}.
 * @returns Field có hộp chọn.
 */
export const Select = ({ label, hint, error, id, required, placeholder, className, children, defaultValue, ...props }: SelectProps) => {
  return (
    <Field label={label} hint={hint} error={error} id={id} required={required}>
      {(a) => (
        <div className="relative">
          <select
            {...a}
            required={required}
            defaultValue={defaultValue ?? (placeholder && props.value === undefined ? "" : undefined)}
            className={cx(surface, "h-(--control-h) cursor-pointer appearance-none pr-10 invalid:text-muted", className)}
            {...props}
          >
            {placeholder && (
              <option value="" disabled>
                {placeholder}
              </option>
            )}
            {children}
          </select>
          <CaretUpDownIcon aria-hidden className="pointer-events-none absolute top-1/2 right-3 size-5 -translate-y-1/2 text-muted" />
        </div>
      )}
    </Field>
  );
}

/** Thuộc tính của {@link QuantityInput}. */
export type QuantityInputProps = FieldBase & {
  /** Giá trị hiện tại. */
  value: number;
  /** Gọi với giá trị mới đã kẹp trong [min, max]. */
  onChange: (value: number) => void;
  /** Nhỏ nhất; mặc định 1. */
  min?: number;
  /** Lớn nhất (vd. tồn kho); không đặt thì không giới hạn. */
  max?: number;
  /** id ô số. */
  id?: string;
  /** Khoá cả nhóm. */
  disabled?: boolean;
};

/**
 * Chọn số lượng: nút −/+ 44px hai bên ô số; gõ trực tiếp được, kẹp vào [min, max] khi rời ô.
 *
 * @param props - {@link QuantityInputProps}.
 * @returns Field có bộ chọn số lượng.
 */
export const QuantityInput = ({ label, hint, error, id, value, onChange, min = 1, max, disabled }: QuantityInputProps) => {
  const [draft, setDraft] = useState<string | null>(null);
  const clamp = (n: number) => Math.min(max ?? Infinity, Math.max(min, Number.isFinite(n) ? Math.trunc(n) : min));
  const step = "flex h-(--control-h) w-11 shrink-0 items-center justify-center text-foreground transition-colors hover:bg-surface-sunken disabled:opacity-40 [&_svg]:size-4";
  return (
    <Field label={label} hint={hint} error={error} id={id}>
      {(a) => (
        <div className="flex w-fit items-stretch overflow-hidden rounded-box border border-border-strong bg-surface shadow-box-1 has-disabled:shadow-none focus-within:border-primary focus-within:ring-4 focus-within:ring-focus/25">
          <button type="button" aria-label="Giảm" className={step} disabled={disabled || value <= min} onClick={() => onChange(clamp(value - 1))}>
            <MinusIcon aria-hidden weight="bold" />
          </button>
          <input
            {...a}
            inputMode="numeric"
            disabled={disabled}
            value={draft ?? value}
            onChange={(e) => setDraft(e.target.value.replace(/\D/g, ""))}
            onBlur={() => {
              if (draft !== null) onChange(clamp(Number(draft)));
              setDraft(null);
            }}
            className="w-14 border-x border-border bg-surface text-center text-base font-semibold tabular-nums outline-none disabled:bg-surface-sunken disabled:text-muted"
          />
          <button type="button" aria-label="Tăng" className={step} disabled={disabled || (max !== undefined && value >= max)} onClick={() => onChange(clamp(value + 1))}>
            <PlusIcon aria-hidden weight="bold" />
          </button>
        </div>
      )}
    </Field>
  );
}

/** Thuộc tính của {@link Checkbox}. */
export type CheckboxProps = Omit<ComponentProps<typeof RCheckbox.Root>, "children"> & FieldBase;

/**
 * Ô đánh dấu (Radix Checkbox) có nhãn bên phải; cả dòng là vùng chạm 44px. Gửi được trong form qua `name`.
 *
 * @param props - {@link CheckboxProps}.
 * @returns Dòng checkbox + nhãn + gợi ý/lỗi.
 */
export const Checkbox = ({ label, hint, error, id, className, ...props }: CheckboxProps) => {
  const auto = useId();
  const fid = id ?? auto;
  const hintId = hint ? `${fid}-hint` : undefined;
  const errorId = error ? `${fid}-error` : undefined;
  return (
    <div className="flex flex-col gap-1">
      <div className="flex min-h-11 items-center gap-3">
        <RCheckbox.Root
          id={fid}
          aria-invalid={error ? true : undefined}
          aria-describedby={cx(hintId, errorId) || undefined}
          className={cx(
            "peer flex size-5 shrink-0 items-center justify-center rounded-sm border border-border-strong bg-surface shadow-inset transition-colors",
            "data-[state=checked]:border-primary data-[state=checked]:bg-primary data-[state=checked]:text-primary-foreground data-[state=checked]:shadow-none",
            "aria-invalid:border-danger disabled:cursor-not-allowed disabled:bg-surface-sunken disabled:opacity-60",
            className,
          )}
          {...props}
        >
          <RCheckbox.Indicator className="data-[state=checked]:animate-fade-in">
            <CheckIcon aria-hidden weight="bold" className="size-3.5" />
          </RCheckbox.Indicator>
        </RCheckbox.Root>
        <label htmlFor={fid} className="cursor-pointer text-sm font-medium peer-disabled:cursor-not-allowed peer-disabled:opacity-60">
          {label}
        </label>
      </div>
      <div className="pl-8">
        <Messages hint={hint} error={error} hintId={hintId} errorId={errorId} />
      </div>
    </div>
  );
}

/** Thuộc tính của {@link Switch}. */
export type SwitchProps = Omit<ComponentProps<typeof RSwitch.Root>, "children"> & FieldBase;

/**
 * Công tắc bật/tắt có hiệu lực ngay (vd. nhận thông báo); nhãn bên trái, công tắc bên phải.
 *
 * @param props - {@link SwitchProps}.
 * @returns Dòng nhãn + công tắc.
 */
export const Switch = ({ label, hint, error, id, className, ...props }: SwitchProps) => {
  const auto = useId();
  const fid = id ?? auto;
  const hintId = hint ? `${fid}-hint` : undefined;
  const errorId = error ? `${fid}-error` : undefined;
  return (
    <div className="flex min-h-11 items-center justify-between gap-4">
      <div className="flex flex-col gap-0.5">
        <label htmlFor={fid} className="cursor-pointer text-sm font-medium">
          {label}
        </label>
        <Messages hint={hint} error={error} hintId={hintId} errorId={errorId} />
      </div>
      <RSwitch.Root
        id={fid}
        aria-invalid={error ? true : undefined}
        aria-describedby={cx(hintId, errorId) || undefined}
        className={cx(
          "relative inline-flex h-7 w-12 shrink-0 items-center rounded-full border border-border-strong bg-surface-sunken p-0.5 shadow-inset transition-colors",
          "data-[state=checked]:border-primary data-[state=checked]:bg-primary disabled:cursor-not-allowed disabled:opacity-50",
          className,
        )}
        {...props}
      >
        <RSwitch.Thumb className="block size-5.5 rounded-full bg-surface-raised shadow-box-1 transition-transform duration-200 ease-out-box data-[state=checked]:translate-x-5" />
      </RSwitch.Root>
    </div>
  );
}

/** Một lựa chọn của {@link RadioGroup}. */
export type RadioOption = {
  /** Giá trị gửi đi. */
  value: string;
  /** Nhãn chính. */
  label: ReactNode;
  /** Dòng mô tả phụ (vd. thời gian giao, phí). */
  description?: ReactNode;
  /** Nội dung canh phải (vd. giá). */
  aside?: ReactNode;
  /** Khoá lựa chọn này. */
  disabled?: boolean;
};

/** Thuộc tính của {@link RadioGroup}. */
export type RadioGroupProps = Omit<ComponentProps<typeof RRadioGroup.Root>, "children"> &
  FieldBase & {
    /** Các lựa chọn. */
    options: RadioOption[];
    /** `card`: mỗi lựa chọn là thẻ hộp (phương thức giao hàng, thanh toán); `list`: dòng gọn. Mặc định `list`. */
    variant?: "list" | "card";
  };

/**
 * Nhóm chọn một (Radix RadioGroup): phím mũi tên di chuyển giữa các lựa chọn; biến thể `card` cho lựa chọn có mô tả và giá.
 *
 * @param props - {@link RadioGroupProps}.
 * @returns Nhóm radio có tiêu đề.
 */
export const RadioGroup = ({ label, hint, error, options, variant = "list", className, ...props }: RadioGroupProps) => {
  const gid = useId();
  const hintId = hint ? `${gid}-hint` : undefined;
  const errorId = error ? `${gid}-error` : undefined;
  const card = variant === "card";
  return (
    <div className="flex flex-col gap-1.5">
      <span id={`${gid}-label`} className="text-sm font-medium">
        {label}
      </span>
      <RRadioGroup.Root
        aria-labelledby={`${gid}-label`}
        aria-invalid={error ? true : undefined}
        aria-describedby={cx(hintId, errorId) || undefined}
        className={cx("flex flex-col", card ? "gap-2" : "gap-0", className)}
        {...props}
      >
        {options.map((o) => {
          const oid = `${gid}-${o.value}`;
          return (
            <label
              key={o.value}
              htmlFor={oid}
              className={cx(
                "group flex cursor-pointer items-center gap-3 has-disabled:cursor-not-allowed has-disabled:opacity-50",
                card
                  ? "rounded-lg border border-border bg-surface p-box shadow-box-1 transition-[border-color,box-shadow,background-color] hover:border-border-strong has-data-[state=checked]:border-primary has-data-[state=checked]:bg-accent/40 has-data-[state=checked]:ring-1 has-data-[state=checked]:ring-primary"
                  : "min-h-11",
              )}
            >
              <RRadioGroup.Item
                id={oid}
                value={o.value}
                disabled={o.disabled}
                className="flex size-5 shrink-0 items-center justify-center rounded-full border border-border-strong bg-surface shadow-inset transition-colors data-[state=checked]:border-primary data-[state=checked]:bg-primary data-[state=checked]:shadow-none"
              >
                <RRadioGroup.Indicator className="block size-2 rounded-full bg-primary-foreground data-[state=checked]:animate-fade-in" />
              </RRadioGroup.Item>
              <span className="flex flex-1 flex-col">
                <span className="text-sm font-medium">{o.label}</span>
                {o.description && <span className="text-sm text-muted">{o.description}</span>}
              </span>
              {o.aside && <span className="text-sm font-semibold tabular-nums">{o.aside}</span>}
            </label>
          );
        })}
      </RRadioGroup.Root>
      <Messages hint={hint} error={error} hintId={hintId} errorId={errorId} />
    </div>
  );
}

/**
 * Chuẩn hoá để tìm kiếm tiếng Việt không dấu: bỏ dấu, đ → d, chữ thường.
 *
 * @param s - Chuỗi gốc.
 * @returns Chuỗi đã bỏ dấu.
 */
export function foldVietnamese(s: string) {
  return s.normalize("NFD").replace(/\p{M}/gu, "").replace(/đ/g, "d").replace(/Đ/g, "d").toLowerCase();
}

// Khớp khi mọi từ gõ vào đều xuất hiện (không dấu, không phân biệt thứ tự): "noi ha" khớp "Hà Nội".
const vnFilter = (value: string, search: string, keywords?: string[]) => {
  const hay = foldVietnamese([value, ...(keywords ?? [])].join(" "));
  return foldVietnamese(search).split(/\s+/).filter(Boolean).every((w) => hay.includes(w)) ? 1 : 0;
};

/** Một lựa chọn của {@link Combobox}. */
export type ComboboxOption = {
  /** Giá trị gửi đi (duy nhất). */
  value: string;
  /** Nhãn hiển thị, cũng là nội dung tìm kiếm. */
  label: string;
  /** Dòng mô tả phụ, cũng được tìm kiếm. */
  description?: string;
  /** Tên nhóm; các lựa chọn cùng nhóm hiển thị dưới một tiêu đề. */
  group?: string;
  /** Khoá lựa chọn này. */
  disabled?: boolean;
};

/** Thuộc tính của {@link Combobox}. */
export type ComboboxProps = FieldBase & {
  /** Danh sách lựa chọn. */
  options: ComboboxOption[];
  /** Giá trị đang chọn (điều khiển; `null` = chưa chọn). */
  value?: string | null;
  /** Giá trị ban đầu khi tự quản. */
  defaultValue?: string | null;
  /** Gọi khi chọn hoặc bỏ chọn. */
  onChange?: (value: string | null) => void;
  /** id của trigger (gắn với nhãn). */
  id?: string;
  /** Tên trường khi gửi form (input ẩn). */
  name?: string;
  /** Bắt buộc (dấu `*`, ẩn nút bỏ chọn). */
  required?: boolean;
  /** Khoá. */
  disabled?: boolean;
  /** Chữ khi chưa chọn. */
  placeholder?: string;
  /** Chữ trong ô tìm kiếm. */
  searchPlaceholder?: string;
  /** Nội dung khi không có kết quả. */
  empty?: ReactNode;
};

const footerLink = "h-9 rounded-box px-3 text-left text-sm text-muted transition-colors hover:bg-surface-sunken hover:text-foreground";

/**
 * Danh sách có ô tìm kiếm dùng chung cho {@link Combobox} và {@link MultiCombobox}: lọc không dấu, nhóm, mô tả phụ, phím mũi tên/Enter.
 *
 * @param props - Lựa chọn; `isSelected`/`onSelect` theo chế độ chọn; `multiple` hiện ô đánh dấu; `isDisabled` khoá thêm (vd. đã đủ `max`); `footer` thanh dưới.
 * @returns Khối cmdk.
 */
const ComboList = ({
  label,
  options,
  isSelected,
  onSelect,
  multiple,
  isDisabled,
  searchPlaceholder,
  empty,
  footer,
}: {
  label: ReactNode;
  options: ComboboxOption[];
  isSelected: (v: string) => boolean;
  onSelect: (v: string) => void;
  multiple?: boolean;
  isDisabled?: (o: ComboboxOption) => boolean;
  searchPlaceholder: string;
  empty: ReactNode;
  footer?: ReactNode;
}) => {
  const groups = [...new Set(options.map((o) => o.group))];
  return (
    <Command filter={vnFilter} loop label={typeof label === "string" ? label : undefined}>
      <div className="flex items-center gap-2 border-b border-border px-3">
        <MagnifyingGlassIcon aria-hidden className="size-4 shrink-0 text-muted" />
        <Command.Input placeholder={searchPlaceholder} className="h-11 w-full bg-transparent text-base outline-none placeholder:text-muted" />
      </div>
      <Command.List aria-multiselectable={multiple || undefined} className="max-h-72 overflow-y-auto overscroll-contain p-1">
        <Command.Empty className="px-3 py-6 text-center text-sm text-muted">{empty}</Command.Empty>
        {groups.map((g) => (
          <Command.Group
            key={g ?? ""}
            heading={g}
            className="[&_[cmdk-group-heading]]:px-3 [&_[cmdk-group-heading]]:pt-2 [&_[cmdk-group-heading]]:pb-1 [&_[cmdk-group-heading]]:text-xs [&_[cmdk-group-heading]]:font-medium [&_[cmdk-group-heading]]:text-muted"
          >
            {options
              .filter((o) => o.group === g)
              .map((o) => {
                const on = isSelected(o.value);
                return (
                  <Command.Item
                    key={o.value}
                    value={o.value}
                    keywords={[o.label, o.description ?? ""]}
                    disabled={o.disabled || isDisabled?.(o)}
                    onSelect={() => onSelect(o.value)}
                    className="flex min-h-11 cursor-pointer items-center gap-3 rounded-box px-3 py-1.5 text-sm transition-colors data-[disabled=true]:pointer-events-none data-[disabled=true]:opacity-50 data-[selected=true]:bg-surface-sunken"
                  >
                    {multiple && (
                      <span
                        aria-hidden
                        className={cx(
                          "flex size-5 shrink-0 items-center justify-center rounded-sm border transition-colors",
                          on ? "border-primary bg-primary text-primary-foreground" : "border-border-strong bg-surface shadow-inset",
                        )}
                      >
                        {on && <CheckIcon weight="bold" className="size-3.5" />}
                      </span>
                    )}
                    <span className="flex flex-1 flex-col">
                      <span className={cx(on && "font-semibold")}>{o.label}</span>
                      {o.description && <span className="text-xs text-muted">{o.description}</span>}
                    </span>
                    {on && <span className="sr-only">, đã chọn</span>}
                    {!multiple && on && <CheckIcon aria-hidden weight="bold" className="size-4 shrink-0 text-primary" />}
                  </Command.Item>
                );
              })}
          </Command.Group>
        ))}
      </Command.List>
      {footer && <div className="flex items-center gap-1 border-t border-border p-1">{footer}</div>}
    </Command>
  );
}

/**
 * Hộp chọn tuỳ biến có tìm kiếm (tỉnh/thành, sản phẩm, nhân viên…): gõ tiếng Việt có dấu hoặc không dấu đều tìm được,
 * phím mũi tên/Enter/Esc, nhóm lựa chọn, mô tả phụ. Danh sách ngắn không cần tìm kiếm thì dùng {@link Select} native.
 *
 * @param props - {@link ComboboxProps}.
 * @returns Field chọn có tìm kiếm.
 */
export const Combobox = ({
  label,
  hint,
  error,
  options,
  value,
  defaultValue,
  onChange,
  id,
  name,
  required,
  disabled,
  placeholder = "Chọn…",
  searchPlaceholder = "Tìm kiếm…",
  empty = "Không tìm thấy kết quả",
}: ComboboxProps) => {
  const [selected, setSelected] = useControllable(value, defaultValue, onChange);
  const [open, setOpen] = useState(false);
  const current = options.find((o) => o.value === selected);
  const pick = (v: string | null) => {
    setSelected(v);
    setOpen(false);
  };
  return (
    <Field label={label} hint={hint} error={error} id={id} required={required}>
      {(a) => (
        <Popover.Root open={open} onOpenChange={setOpen}>
          <Trigger a={{ ...a, role: "combobox", "aria-expanded": open }} text={current?.label} placeholder={placeholder} icon={<CaretUpDownIcon />} disabled={disabled} />
          {name && <input type="hidden" name={name} value={selected ?? ""} />}
          <Panel label={typeof label === "string" ? label : "Chọn"} matchWidth>
            <ComboList
              label={label}
              options={options}
              isSelected={(v) => v === selected}
              onSelect={pick}
              searchPlaceholder={searchPlaceholder}
              empty={empty}
              footer={
                selected && !required ? (
                  <button type="button" className={footerLink} onClick={() => pick(null)}>
                    Bỏ chọn
                  </button>
                ) : undefined
              }
            />
          </Panel>
        </Popover.Root>
      )}
    </Field>
  );
}

/** Thuộc tính của {@link MultiCombobox}. */
export type MultiComboboxProps = Omit<ComboboxProps, "value" | "defaultValue" | "onChange"> & {
  /** Các giá trị đang chọn (điều khiển). */
  value?: string[];
  /** Các giá trị ban đầu khi tự quản. */
  defaultValue?: string[];
  /** Gọi với danh sách mới mỗi lần chọn/bỏ. */
  onChange?: (value: string[]) => void;
  /** Số lựa chọn tối đa; đạt tối đa thì các mục chưa chọn bị khoá. */
  max?: number;
};

/**
 * Chọn nhiều có tìm kiếm (gắn thẻ, lọc nhiều kho/trạng thái): bấm để chọn/bỏ, popover vẫn mở; các mục đã chọn hiện thành chip có nút bỏ
 * dưới ô, xuống dòng khi nhiều. Gửi form: mỗi giá trị một input ẩn cùng `name` (đọc bằng `FormData.getAll`).
 *
 * @param props - {@link MultiComboboxProps}.
 * @returns Field chọn nhiều.
 */
export const MultiCombobox = ({
  label,
  hint,
  error,
  options,
  value,
  defaultValue,
  onChange,
  id,
  name,
  required,
  disabled,
  placeholder = "Chọn…",
  searchPlaceholder = "Tìm kiếm…",
  empty = "Không tìm thấy kết quả",
  max,
}: MultiComboboxProps) => {
  const [picked, setPicked] = useControllable<string[]>(value, defaultValue, (v) => onChange?.(v ?? []));
  const list = picked ?? [];
  const [open, setOpen] = useState(false);
  const byValue = new Map(options.map((o) => [o.value, o]));
  const chosen = list.map((v) => byValue.get(v)).filter((o): o is ComboboxOption => !!o);
  const toggle = (v: string) => setPicked(list.includes(v) ? list.filter((x) => x !== v) : [...list, v]);
  const full = max !== undefined && list.length >= max;
  const summary = chosen.length === 0 ? undefined : chosen.length <= 2 ? chosen.map((o) => o.label).join(", ") : `Đã chọn ${chosen.length}`;
  return (
    <Field label={label} hint={hint} error={error} id={id} required={required}>
      {(a) => (
        <div className="flex flex-col gap-2">
          <Popover.Root open={open} onOpenChange={setOpen}>
            <Trigger a={{ ...a, role: "combobox", "aria-expanded": open }} text={summary} placeholder={placeholder} icon={<CaretUpDownIcon />} disabled={disabled} />
            {name && list.map((v) => <input key={v} type="hidden" name={name} value={v} />)}
            <Panel label={typeof label === "string" ? label : "Chọn"} matchWidth>
              <ComboList
                label={label}
                options={options}
                multiple
                isSelected={(v) => list.includes(v)}
                isDisabled={(o) => full && !list.includes(o.value)}
                onSelect={toggle}
                searchPlaceholder={searchPlaceholder}
                empty={empty}
                footer={
                  <>
                    <span className="mr-auto px-3 text-sm text-muted tabular-nums" aria-live="polite">
                      {max !== undefined ? `Đã chọn ${list.length}/${max}` : `Đã chọn ${list.length}`}
                    </span>
                    {list.length > 0 && (
                      <button type="button" className={footerLink} onClick={() => setPicked([])}>
                        Bỏ chọn tất cả
                      </button>
                    )}
                    <button
                      type="button"
                      className="h-9 rounded-box bg-primary px-3 text-sm font-semibold text-primary-foreground transition-colors hover:bg-primary-hover"
                      onClick={() => setOpen(false)}
                    >
                      Xong
                    </button>
                  </>
                }
              />
            </Panel>
          </Popover.Root>
          {chosen.length > 0 && (
            <ul aria-label="Đã chọn" className="flex flex-wrap gap-1.5">
              {chosen.map((o) => (
                <li key={o.value} className="flex h-8 items-center gap-0.5 rounded-sm border border-border bg-accent pl-2.5 text-sm font-medium text-accent-foreground">
                  {o.label}
                  <button
                    type="button"
                    aria-label={`Bỏ ${o.label}`}
                    disabled={disabled}
                    onClick={() => toggle(o.value)}
                    className="flex size-8 items-center justify-center rounded-sm transition-colors hover:bg-primary/15 disabled:opacity-50"
                  >
                    <XIcon aria-hidden weight="bold" className="size-3.5" />
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </Field>
  );
}
