import { expect, test } from "vitest";
import { browserTimeZone, DEFAULT_TIME_ZONE, formatDateTime, formatPrice as format } from "./format";

const at = "2026-11-28T17:30:00Z";

test("formatDateTime: theo múi giờ tài khoản/trình duyệt, mặc định giờ Việt Nam; múi giờ sai rơi về mặc định", () => {
  expect(formatDateTime(at, { locale: "vi" })).toBe("00:30 29/11/2026");
  expect(formatDateTime(at, { locale: "vi", timeZone: "Europe/Berlin" })).toBe("18:30 28/11/2026");
  expect(formatDateTime(new Date(at), { locale: "en", timeZone: "UTC" })).toMatch(/Nov 28, 2026.*5:30\sPM/);
  expect(formatDateTime(at, { locale: "vi", timeZone: "Mars/Olympus" })).toBe("00:30 29/11/2026");
  expect(DEFAULT_TIME_ZONE).toBe("Asia/Ho_Chi_Minh");
  expect(browserTimeZone()).toBeTruthy();
});

test("formatPrice: luôn hiện VND; tiền tệ khác có tỷ giá thì kèm ước tính, không có tỷ giá thì không", () => {
  // Intl đặt khoảng trắng không ngắt (U+00A0) giữa số và ký hiệu: giữ nguyên để giá không bị xuống dòng.
  const formatPrice = (...a: Parameters<typeof format>) =>
    Object.fromEntries(Object.entries(format(...a)).map(([k, v]) => [k, v.replace(/ /g, " ")]));
  const usd = { code: "USD", minorUnit: 2, vndPerUnit: 25000 };
  expect(formatPrice(1_250_000, { locale: "vi" })).toEqual({ vnd: "1.250.000 ₫" });
  expect(formatPrice(1_250_000, { locale: "en", currency: usd })).toEqual({ vnd: "₫1,250,000", estimate: "≈ $50.00" });
  expect(formatPrice(1_000, { locale: "vi", currency: { code: "JPY", minorUnit: 0, vndPerUnit: 170 } }).estimate).toMatch(/^≈ 6\s?(¥|JPY)$/);
  expect(formatPrice(1_000, { locale: "en", currency: { code: "EUR", minorUnit: 2, vndPerUnit: null } })).toEqual({ vnd: "₫1,000" });
  expect(formatPrice(1_000, { locale: "vi", currency: { code: "VND", minorUnit: 0, vndPerUnit: 1 } })).toEqual({ vnd: "1.000 ₫" });
});
