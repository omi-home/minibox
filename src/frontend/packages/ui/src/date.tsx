"use client";
/*
 * Chọn ngày/giờ tiếng Việt: lịch react-day-picker (locale vi, tuần bắt đầu thứ Hai) trong Radix Popover, định dạng dd/mm/yyyy và giờ 24h
 * bất kể ngôn ngữ trình duyệt (bộ chọn native theo ngôn ngữ trình duyệt nên ra mm/dd/yyyy, AM/PM). Gửi form qua input ẩn theo định dạng
 * của input native: ngày `yyyy-mm-dd`, ngày giờ `yyyy-mm-ddTHH:mm`, giờ `HH:mm` (giờ địa phương).
 */
import { CalendarBlankIcon, CaretLeftIcon, CaretRightIcon, ClockIcon } from "@phosphor-icons/react";
import { Popover } from "radix-ui";
import { useEffect, useRef, useState, useSyncExternalStore } from "react";
import { DayPicker, type DateRange, type DayPickerProps, type Matcher } from "react-day-picker";
import { vi } from "react-day-picker/locale/vi";
import { cx, Field, Panel, Trigger, useControllable, type FieldBase } from "./field";

const pad = (n: number) => String(n).padStart(2, "0");
const dateText = new Intl.DateTimeFormat("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric" });
const isoDate = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
const hhmm = (h: number, m: number) => `${pad(h)}:${pad(m)}`;
const startOfDay = (d: Date) => new Date(d.getFullYear(), d.getMonth(), d.getDate());
const addDays = (d: Date, n: number) => new Date(d.getFullYear(), d.getMonth(), d.getDate() + n);

const subscribeWide = (cb: () => void) => {
  const mq = typeof window !== "undefined" && window.matchMedia ? window.matchMedia("(width >= 40rem)") : null;
  mq?.addEventListener("change", cb);
  return () => mq?.removeEventListener("change", cb);
};
/**
 * Màn hình từ tablet trở lên (≥ 640px) — để lịch khoảng ngày hiện 2 tháng.
 *
 * @returns true khi rộng ≥ 640px.
 */
function useWide() {
  return useSyncExternalStore(
    subscribeWide,
    () => (typeof window !== "undefined" && window.matchMedia ? window.matchMedia("(width >= 40rem)").matches : false),
    () => false,
  );
}

// Trạng thái ngày gắn lên ô (td) bằng tên lớp đánh dấu; nút ngày bên trong đổi kiểu theo các lớp này để không xung đột thứ tự CSS.
const calendarClassNames = {
  root: "relative p-3",
  months: "relative flex flex-col gap-6 sm:flex-row",
  month: "flex flex-col gap-2",
  month_caption: "flex h-10 items-center justify-center px-12",
  caption_label: "text-sm font-semibold first-letter:uppercase",
  nav: "absolute inset-x-3 top-3 z-10 flex justify-between",
  button_previous: "flex size-10 items-center justify-center rounded-box text-muted transition-colors hover:bg-surface-sunken hover:text-foreground aria-disabled:pointer-events-none aria-disabled:opacity-30",
  button_next: "flex size-10 items-center justify-center rounded-box text-muted transition-colors hover:bg-surface-sunken hover:text-foreground aria-disabled:pointer-events-none aria-disabled:opacity-30",
  month_grid: "border-collapse",
  weekday: "h-8 w-10 text-xs font-medium text-muted",
  day: "p-0 text-center text-sm",
  day_button: cx(
    "flex size-10 items-center justify-center rounded-box tabular-nums transition-colors hover:bg-surface-sunken",
    "[.is-today>&]:font-bold [.is-today:not(.is-selected)>&]:text-primary",
    "[.is-outside>&]:text-muted/60",
    "[.is-selected:not(.is-mid)>&]:bg-primary [.is-selected:not(.is-mid)>&]:font-semibold [.is-selected:not(.is-mid)>&]:text-primary-foreground [.is-selected:not(.is-mid)>&]:shadow-box-1",
    "[.is-mid>&]:rounded-none [.is-mid>&]:font-medium [.is-mid>&]:text-accent-foreground [.is-mid>&]:hover:bg-primary/10",
    "[.is-disabled>&]:pointer-events-none [.is-disabled>&]:text-muted/50 [.is-disabled>&]:line-through",
  ),
  today: "is-today",
  outside: "is-outside",
  disabled: "is-disabled",
  selected: "is-selected",
  range_start: "is-start rounded-l-box bg-accent",
  range_middle: "is-mid bg-accent",
  range_end: "is-end rounded-r-box bg-accent",
  hidden: "invisible",
};

/** Thuộc tính của {@link Calendar}: mọi thuộc tính của react-day-picker. */
export type CalendarProps = DayPickerProps;

/**
 * Lịch tiếng Việt (react-day-picker) đã tạo kiểu theo token: tiêu đề "Tháng 11 / 2026", tuần bắt đầu thứ Hai, hiện ngày tháng kề.
 * Dùng trực tiếp khi cần lịch nằm sẵn trên trang (vd. lịch giao hàng); trong form dùng {@link DatePicker}/{@link DateRangePicker}.
 *
 * @param props - {@link CalendarProps}.
 * @returns Lịch.
 */
export const Calendar = (props: CalendarProps) => {
  return (
    <DayPicker
      locale={vi}
      showOutsideDays
      classNames={calendarClassNames}
      formatters={{ formatCaption: (d: Date) => `Tháng ${d.getMonth() + 1} / ${d.getFullYear()}` }}
      labels={{ labelGrid: (d: Date) => `Tháng ${d.getMonth() + 1} năm ${d.getFullYear()}` }}
      components={{
        Chevron: ({ orientation }) => (orientation === "left" ? <CaretLeftIcon aria-hidden className="size-5" /> : <CaretRightIcon aria-hidden className="size-5" />),
      }}
      {...props}
    />
  );
}

const footerBtn = "h-9 rounded-box px-3 text-sm font-semibold transition-colors";
const ghostBtn = `${footerBtn} text-muted hover:bg-surface-sunken hover:text-foreground`;
const primaryBtn = `${footerBtn} bg-primary text-primary-foreground hover:bg-primary-hover`;

/** Phần chung của các bộ chọn ngày. */
type PickerBase = FieldBase & {
  /** id của trigger (gắn với nhãn). */
  id?: string;
  /** Tên trường khi gửi form (input ẩn). */
  name?: string;
  /** Bắt buộc (dấu `*`). */
  required?: boolean;
  /** Khoá bộ chọn. */
  disabled?: boolean;
  /** Chữ khi chưa chọn. */
  placeholder?: string;
  /** Ngày sớm nhất chọn được. */
  min?: Date;
  /** Ngày muộn nhất chọn được. */
  max?: Date;
};

const limits = (min?: Date, max?: Date): Matcher[] => [
  ...(min ? [{ before: startOfDay(min) }] : []),
  ...(max ? [{ after: startOfDay(max) }] : []),
];

/** Thuộc tính của {@link DatePicker}. */
export type DatePickerProps = PickerBase & {
  /** Ngày đang chọn (điều khiển; `null` = chưa chọn). */
  value?: Date | null;
  /** Ngày ban đầu khi tự quản. */
  defaultValue?: Date | null;
  /** Gọi khi chọn hoặc xoá. */
  onChange?: (value: Date | null) => void;
};

/**
 * Chọn một ngày: lịch tiếng Việt, hiển thị dd/mm/yyyy; chọn xong tự đóng. Có nút "Hôm nay" và "Xoá".
 *
 * @param props - {@link DatePickerProps}.
 * @returns Field chọn ngày.
 */
export const DatePicker = ({ label, hint, error, id, name, required, disabled, placeholder = "Chọn ngày", min, max, value, defaultValue, onChange }: DatePickerProps) => {
  const [date, setDate] = useControllable(value, defaultValue, onChange);
  const [open, setOpen] = useState(false);
  const pick = (d: Date | null) => {
    setDate(d);
    setOpen(false);
  };
  return (
    <Field label={label} hint={hint} error={error} id={id} required={required}>
      {(a) => (
        <Popover.Root open={open} onOpenChange={setOpen}>
          <Trigger a={a} text={date ? dateText.format(date) : undefined} placeholder={placeholder} icon={<CalendarBlankIcon />} disabled={disabled} />
          {name && <input type="hidden" name={name} value={date ? isoDate(date) : ""} />}
          <Panel label={typeof label === "string" ? label : "Chọn ngày"}>
            <Calendar
              mode="single"
              selected={date ?? undefined}
              defaultMonth={date ?? undefined}
              onSelect={(d) => pick(d ?? null)}
              disabled={limits(min, max)}
              startMonth={min}
              endMonth={max}
              autoFocus
            />
            <div className="flex justify-between border-t border-border px-3 py-2">
              <button type="button" className={ghostBtn} onClick={() => pick(null)}>
                Xoá
              </button>
              <button type="button" className={ghostBtn} onClick={() => pick(startOfDay(new Date()))}>
                Hôm nay
              </button>
            </div>
          </Panel>
        </Popover.Root>
      )}
    </Field>
  );
}

/** Một khoảng chọn nhanh của {@link DateRangePicker}. */
export type RangePreset = {
  /** Nhãn nút. */
  label: string;
  /** Tính khoảng tại thời điểm bấm. */
  range: () => DateRange;
};

/** Khoảng chọn nhanh mặc định cho báo cáo. */
export const defaultRangePresets: RangePreset[] = [
  { label: "Hôm nay", range: () => ({ from: startOfDay(new Date()), to: startOfDay(new Date()) }) },
  { label: "7 ngày qua", range: () => ({ from: addDays(new Date(), -6), to: startOfDay(new Date()) }) },
  { label: "30 ngày qua", range: () => ({ from: addDays(new Date(), -29), to: startOfDay(new Date()) }) },
  {
    label: "Tháng này",
    range: () => {
      const n = new Date();
      return { from: new Date(n.getFullYear(), n.getMonth(), 1), to: startOfDay(n) };
    },
  },
  {
    label: "Tháng trước",
    range: () => {
      const n = new Date();
      return { from: new Date(n.getFullYear(), n.getMonth() - 1, 1), to: new Date(n.getFullYear(), n.getMonth(), 0) };
    },
  },
];

/** Thuộc tính của {@link DateRangePicker}. */
export type DateRangePickerProps = Omit<PickerBase, "name"> & {
  /** Khoảng đang chọn (điều khiển; `null` = chưa chọn). */
  value?: DateRange | null;
  /** Khoảng ban đầu khi tự quản. */
  defaultValue?: DateRange | null;
  /** Gọi khi bấm "Áp dụng" (khoảng đủ hai đầu) hoặc "Xoá". */
  onChange?: (value: DateRange | null) => void;
  /** Tên trường ngày bắt đầu khi gửi form. */
  nameFrom?: string;
  /** Tên trường ngày kết thúc khi gửi form. */
  nameTo?: string;
  /** Khoảng chọn nhanh; mặc định {@link defaultRangePresets}, `[]` để ẩn. */
  presets?: RangePreset[];
};

const rangeText = (r: DateRange | null | undefined) =>
  r?.from ? `${dateText.format(r.from)} → ${r.to ? dateText.format(r.to) : "…"}` : undefined;
const nights = (r: DateRange) => (r.from && r.to ? Math.round((startOfDay(r.to).getTime() - startOfDay(r.from).getTime()) / 864e5) + 1 : 0);

/**
 * Chọn khoảng ngày (báo cáo, lọc đơn): 2 tháng cạnh nhau từ tablet, 1 tháng trên điện thoại; khoảng chọn nhanh;
 * chọn nháp trong popover, chỉ ghi nhận khi bấm "Áp dụng".
 *
 * @param props - {@link DateRangePickerProps}.
 * @returns Field chọn khoảng ngày.
 */
export const DateRangePicker = ({
  label,
  hint,
  error,
  id,
  nameFrom,
  nameTo,
  required,
  disabled,
  placeholder = "Chọn khoảng ngày",
  min,
  max,
  value,
  defaultValue,
  onChange,
  presets = defaultRangePresets,
}: DateRangePickerProps) => {
  const [range, setRange] = useControllable(value, defaultValue, onChange);
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState<DateRange | undefined>();
  const [month, setMonth] = useState<Date | undefined>();
  const wide = useWide();
  const onOpenChange = (o: boolean) => {
    setOpen(o);
    if (o) {
      setDraft(range ?? undefined);
      setMonth(range?.from ?? undefined);
    }
  };
  const commit = (r: DateRange | null) => {
    setRange(r);
    setOpen(false);
  };
  const days = draft ? nights(draft) : 0;
  return (
    <Field label={label} hint={hint} error={error} id={id} required={required}>
      {(a) => (
        <Popover.Root open={open} onOpenChange={onOpenChange}>
          <Trigger a={a} text={rangeText(range)} placeholder={placeholder} icon={<CalendarBlankIcon />} disabled={disabled} />
          {nameFrom && <input type="hidden" name={nameFrom} value={range?.from ? isoDate(range.from) : ""} />}
          {nameTo && <input type="hidden" name={nameTo} value={range?.to ? isoDate(range.to) : ""} />}
          <Panel label={typeof label === "string" ? label : "Chọn khoảng ngày"}>
            <div className="flex flex-col sm:flex-row">
              {presets.length > 0 && (
                <div className="flex gap-1 overflow-x-auto border-b border-border p-2 sm:w-36 sm:flex-col sm:border-r sm:border-b-0">
                  {presets.map((p) => (
                    <button
                      key={p.label}
                      type="button"
                      className="h-9 shrink-0 rounded-box px-3 text-left text-sm whitespace-nowrap text-foreground transition-colors hover:bg-surface-sunken"
                      onClick={() => {
                        const r = p.range();
                        setDraft(r);
                        setMonth(r.from);
                      }}
                    >
                      {p.label}
                    </button>
                  ))}
                </div>
              )}
              <Calendar
                mode="range"
                resetOnSelect
                selected={draft}
                onSelect={setDraft}
                month={month}
                onMonthChange={setMonth}
                numberOfMonths={wide ? 2 : 1}
                disabled={limits(min, max)}
                startMonth={min}
                endMonth={max}
                autoFocus
              />
            </div>
            <div className="flex flex-wrap items-center gap-2 border-t border-border px-3 py-2">
              <span className="mr-auto text-sm text-muted tabular-nums" aria-live="polite">
                {`${rangeText(draft) ?? "Chọn ngày bắt đầu"}${days > 0 ? ` (${days} ngày)` : ""}`}
              </span>
              <button type="button" className={ghostBtn} onClick={() => commit(null)}>
                Xoá
              </button>
              <button type="button" className={primaryBtn} disabled={!draft?.from || !draft.to} onClick={() => commit(draft ?? null)}>
                Áp dụng
              </button>
            </div>
          </Panel>
        </Popover.Root>
      )}
    </Field>
  );
}

/**
 * Hai cột giờ (0–23) và phút (theo bước), cuộn tới giá trị đang chọn khi mở.
 *
 * @param props - `h`/`m` giờ phút đang chọn (có thể chưa có); `step` bước phút; `onChange` nhận giờ phút mới.
 * @returns Hai cột nút.
 */
const TimeColumns = ({ h, m, step, onChange }: { h?: number; m?: number; step: number; onChange: (h: number, m: number) => void }) => {
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    ref.current?.querySelectorAll<HTMLElement>("[aria-pressed=true]").forEach((el) => el.scrollIntoView?.({ block: "center" }));
  }, []);
  const minutes = Array.from({ length: Math.ceil(60 / step) }, (_, i) => i * step);
  const cell =
    "h-9 w-14 shrink-0 snap-center rounded-box text-sm tabular-nums transition-colors hover:bg-surface-sunken aria-pressed:bg-primary aria-pressed:font-semibold aria-pressed:text-primary-foreground aria-pressed:shadow-box-1";
  const column = (title: string, values: number[], current: number | undefined, unit: string, set: (v: number) => void) => (
    <div className="flex flex-col">
      <span className="px-1 pb-1 text-center text-xs font-medium text-muted">{title}</span>
      <div role="group" aria-label={title} className="flex max-h-72 snap-y flex-col gap-1 overflow-y-auto overscroll-contain p-1">
        {values.map((v) => (
          <button key={v} type="button" aria-pressed={current === v} aria-label={`${v} ${unit}`} className={cell} onClick={() => set(v)}>
            {pad(v)}
          </button>
        ))}
      </div>
    </div>
  );
  return (
    <div ref={ref} className="flex gap-1 p-2">
      {column("Giờ", Array.from({ length: 24 }, (_, i) => i), h, "giờ", (v) => onChange(v, m ?? 0))}
      {column("Phút", minutes, m, "phút", (v) => onChange(h ?? 8, v))}
    </div>
  );
}

/** Thuộc tính của {@link DateTimePicker}. */
export type DateTimePickerProps = PickerBase & {
  /** Thời điểm đang chọn (điều khiển; `null` = chưa chọn). */
  value?: Date | null;
  /** Thời điểm ban đầu khi tự quản. */
  defaultValue?: Date | null;
  /** Gọi khi đổi ngày hoặc giờ. */
  onChange?: (value: Date | null) => void;
  /** Bước phút; mặc định 15. */
  minuteStep?: number;
};

/**
 * Chọn ngày và giờ (hẹn lấy hàng, lịch giao): lịch tiếng Việt cạnh cột giờ/phút 24h, hiển thị `dd/mm/yyyy HH:mm`.
 * Chọn ngày giữ nguyên giờ đã chọn (mặc định 08:00); chọn giờ trước thì lấy ngày hôm nay.
 *
 * @param props - {@link DateTimePickerProps}.
 * @returns Field chọn ngày giờ.
 */
export const DateTimePicker = ({
  label,
  hint,
  error,
  id,
  name,
  required,
  disabled,
  placeholder = "Chọn ngày và giờ",
  min,
  max,
  value,
  defaultValue,
  onChange,
  minuteStep = 15,
}: DateTimePickerProps) => {
  const [dt, setDt] = useControllable(value, defaultValue, onChange);
  const [open, setOpen] = useState(false);
  const at = (d: Date, h: number, m: number) => new Date(d.getFullYear(), d.getMonth(), d.getDate(), h, m);
  return (
    <Field label={label} hint={hint} error={error} id={id} required={required}>
      {(a) => (
        <Popover.Root open={open} onOpenChange={setOpen}>
          <Trigger
            a={a}
            text={dt ? `${dateText.format(dt)} ${hhmm(dt.getHours(), dt.getMinutes())}` : undefined}
            placeholder={placeholder}
            icon={<CalendarBlankIcon />}
            disabled={disabled}
          />
          {name && <input type="hidden" name={name} value={dt ? `${isoDate(dt)}T${hhmm(dt.getHours(), dt.getMinutes())}` : ""} />}
          <Panel label={typeof label === "string" ? label : "Chọn ngày và giờ"}>
            <div className="flex flex-col sm:flex-row">
              <Calendar
                mode="single"
                selected={dt ?? undefined}
                defaultMonth={dt ?? undefined}
                onSelect={(d) => d && setDt(at(d, dt?.getHours() ?? 8, dt?.getMinutes() ?? 0))}
                disabled={limits(min, max)}
                startMonth={min}
                endMonth={max}
                autoFocus
              />
              <div className="border-t border-border sm:border-t-0 sm:border-l">
                <TimeColumns h={dt?.getHours()} m={dt?.getMinutes()} step={minuteStep} onChange={(h, m) => setDt(at(dt ?? new Date(), h, m))} />
              </div>
            </div>
            <div className="flex justify-between border-t border-border px-3 py-2">
              <button
                type="button"
                className={ghostBtn}
                onClick={() => {
                  setDt(null);
                  setOpen(false);
                }}
              >
                Xoá
              </button>
              <button type="button" className={primaryBtn} onClick={() => setOpen(false)}>
                Xong
              </button>
            </div>
          </Panel>
        </Popover.Root>
      )}
    </Field>
  );
}

/** Thuộc tính của {@link TimePicker}. */
export type TimePickerProps = Omit<PickerBase, "min" | "max"> & {
  /** Giờ đang chọn dạng `HH:mm` (điều khiển; `null` = chưa chọn). */
  value?: string | null;
  /** Giờ ban đầu khi tự quản. */
  defaultValue?: string | null;
  /** Gọi khi đổi. */
  onChange?: (value: string | null) => void;
  /** Bước phút; mặc định 15. */
  minuteStep?: number;
};

/**
 * Chọn giờ 24h (giờ mở cửa kho, khung giao): cột giờ/phút trong popover, hiển thị `HH:mm`.
 *
 * @param props - {@link TimePickerProps}.
 * @returns Field chọn giờ.
 */
export const TimePicker = ({ label, hint, error, id, name, required, disabled, placeholder = "Chọn giờ", value, defaultValue, onChange, minuteStep = 15 }: TimePickerProps) => {
  const [time, setTime] = useControllable(value, defaultValue, onChange);
  const [h, m] = time ? time.split(":").map(Number) : [];
  return (
    <Field label={label} hint={hint} error={error} id={id} required={required}>
      {(a) => (
        <Popover.Root>
          <Trigger a={a} text={time ?? undefined} placeholder={placeholder} icon={<ClockIcon />} disabled={disabled} />
          {name && <input type="hidden" name={name} value={time ?? ""} />}
          <Panel label={typeof label === "string" ? label : "Chọn giờ"}>
            <TimeColumns h={h} m={m} step={minuteStep} onChange={(nh, nm) => setTime(hhmm(nh, nm))} />
          </Panel>
        </Popover.Root>
      )}
    </Field>
  );
}
