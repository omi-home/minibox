/** Múi giờ mặc định khi tài khoản chưa chọn và không biết múi giờ trình duyệt (spec §6.6). */
export const DEFAULT_TIME_ZONE = "Asia/Ho_Chi_Minh";

/** Tiền tệ hiển thị kèm tỷ giá, như `GET /currencies` của localization trả về. */
export type DisplayCurrency = {
  /** Mã ISO 4217. */
  code: string;
  /** Số chữ số thập phân khi hiển thị. */
  minorUnit: number;
  /** Số đồng cho 1 đơn vị; `null` khi chưa có tỷ giá (không hiện quy đổi). */
  vndPerUnit: number | string | null;
};

/**
 * Múi giờ của trình duyệt (cho khách chưa đăng nhập).
 *
 * @returns Mã IANA; không xác định được thì {@link DEFAULT_TIME_ZONE}.
 */
export function browserTimeZone(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone || DEFAULT_TIME_ZONE;
}

/**
 * Định dạng thời điểm (DB lưu UTC) theo locale và múi giờ người xem.
 *
 * @param value - Thời điểm, `Date` hoặc chuỗi ISO 8601.
 * @param options - `locale` BCP 47; `timeZone` IANA của tài khoản/trình duyệt (thiếu hoặc sai thì giờ Việt Nam).
 * @returns Ngày giờ; với `vi` dạng `HH:mm dd/MM/yyyy`.
 * @remarks Mốc nghiệp vụ (hạn giao, giờ mở cửa) tính theo múi giờ cửa hàng, không dùng hàm này để tính.
 */
export function formatDateTime(value: Date | string, options: { locale: string; timeZone?: string }): string {
  const date = typeof value === "string" ? new Date(value) : value;
  const format = (timeZone: string) =>
    options.locale.startsWith("vi")
      ? `${part(date, "vi-VN", timeZone, { hour: "2-digit", minute: "2-digit", hourCycle: "h23" })} ${part(date, "vi-VN", timeZone, { day: "2-digit", month: "2-digit", year: "numeric" })}`
      : new Intl.DateTimeFormat(options.locale, { dateStyle: "medium", timeStyle: "short", timeZone }).format(date);
  try {
    return format(options.timeZone ?? DEFAULT_TIME_ZONE);
  } catch {
    return format(DEFAULT_TIME_ZONE);
  }
}

const part = (date: Date, locale: string, timeZone: string, o: Intl.DateTimeFormatOptions) =>
  new Intl.DateTimeFormat(locale, { ...o, timeZone }).format(date);

/**
 * Định dạng giá: luôn là VND (giao dịch tính VND); chọn tiền tệ khác và có tỷ giá thì kèm số quy đổi ước tính.
 *
 * @param vnd - Số tiền VND (số nguyên).
 * @param options - `locale` BCP 47 để định dạng số; `currency` tiền tệ hiển thị người dùng chọn.
 * @returns `vnd` chuỗi giá VND; `estimate` dạng `≈ $50.00`, chỉ có khi quy đổi được.
 * @remarks Giao diện phải ghi rõ `estimate` là ước tính; đổi tiền tệ không đổi URL.
 */
export function formatPrice(vnd: number, options: { locale: string; currency?: DisplayCurrency }): { vnd: string; estimate?: string } {
  const locale = options.locale.startsWith("vi") ? "vi-VN" : options.locale;
  const money = (amount: number, code: string, digits: number) =>
    new Intl.NumberFormat(locale, { style: "currency", currency: code, minimumFractionDigits: digits, maximumFractionDigits: digits }).format(amount);
  const result: { vnd: string; estimate?: string } = { vnd: money(vnd, "VND", 0) };
  const c = options.currency;
  const rate = Number(c?.vndPerUnit);
  if (c && c.code !== "VND" && rate > 0) result.estimate = `≈ ${money(vnd / rate, c.code, c.minorUnit)}`;
  return result;
}
