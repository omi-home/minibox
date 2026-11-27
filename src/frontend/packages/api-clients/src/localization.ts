import type { LocalizationClient } from "./index";

/** Locale mặc định và fallback cuối (spec §6.6). */
export const DEFAULT_LOCALE = "vi";

/** Thân sự kiện `localization.MessagesPublished.v1` (contracts/events). */
export type MessagesPublished = { locale: string; namespace: string; keys: string[] };

/** Cache chuỗi giao diện phía BFF. */
export type MessagesCache = {
  /** Chuỗi đã xuất bản của (locale, namespace), đã áp chuỗi fallback phía localization; lỗi mạng mà chưa có bản cũ thì rỗng. */
  messages(locale: string, namespace: string): Promise<Record<string, string>>;
  /** Mã các locale đang bật; lỗi mạng mà chưa có bản cũ thì chỉ `vi`. */
  locales(): Promise<string[]>;
  /** Xử lý `MessagesPublished`: bỏ cache namespace đó ở mọi locale (locale khác có thể rơi về locale vừa xuất bản). */
  onMessagesPublished(event: MessagesPublished): void;
};

type Entry<T> = { value: T; etag?: string; at: number };

/**
 * Tạo cache chuỗi giao diện trong tiến trình BFF: hết `ttlMs` thì hỏi lại localization bằng `If-None-Match` (304 rẻ), lỗi thì giữ bản cũ.
 *
 * @param client - Client localization.
 * @param options - `ttlMs` thời gian dùng cache không hỏi lại (mặc định 60 giây); `now` đồng hồ (ms) để test.
 * @returns Cache chuỗi và danh sách locale.
 * @remarks ponytail: cache theo tiến trình, nhiều instance BFF mỗi cái tự làm mới; TTL là lưới an toàn khi sự kiện không tới.
 */
export function createMessagesCache(client: LocalizationClient, options: { ttlMs?: number; now?: () => number } = {}): MessagesCache {
  const { ttlMs = 60_000, now = Date.now } = options;
  const messages = new Map<string, Entry<Record<string, string>>>();
  let locales: Entry<string[]> | undefined;

  return {
    async messages(locale, namespace) {
      const key = `${locale}\n${namespace}`;
      const hit = messages.get(key);
      if (hit && now() - hit.at < ttlMs) return hit.value;
      try {
        const { data, response } = await client.GET("/messages/{locale}/{namespace}", {
          params: { path: { locale, namespace } },
          headers: hit?.etag ? { "if-none-match": hit.etag } : {},
        });
        if (response.status === 304 && hit) hit.at = now();
        else if (data) messages.set(key, { value: data.messages, etag: response.headers.get("etag") ?? undefined, at: now() });
      } catch {
        // Giữ bản cũ; lần sau hỏi lại.
      }
      return messages.get(key)?.value ?? {};
    },
    async locales() {
      if (locales && now() - locales.at < ttlMs) return locales.value;
      try {
        const { data } = await client.GET("/locales");
        if (data) locales = { value: data.map((l) => l.code), at: now() };
      } catch {
        // Giữ bản cũ.
      }
      return locales?.value ?? [DEFAULT_LOCALE];
    },
    onMessagesPublished({ namespace }) {
      for (const key of messages.keys()) if (key.endsWith(`\n${namespace}`)) messages.delete(key);
    },
  };
}

/**
 * Chọn locale của request theo thứ tự spec §6.6: URL → cookie → `Accept-Language` → `vi`; chỉ nhận locale đang bật.
 *
 * @param sources - `url` locale ở tiền tố đường dẫn; `cookie` locale người dùng đã chọn; `acceptLanguage` header trình duyệt.
 * @param enabled - Mã các locale đang bật.
 * @returns Mã locale dùng cho request.
 * @remarks `Accept-Language` xét theo `q` giảm dần; `en-US` khớp `en` nếu không có `en-US`. Chuỗi fallback của từng locale do
 * localization áp khi trả chuỗi.
 */
export function resolveLocale(sources: { url?: string; cookie?: string; acceptLanguage?: string }, enabled: string[]): string {
  const byLower = new Map(enabled.map((c) => [c.toLowerCase(), c]));
  const pick = (code?: string) => (code ? byLower.get(code.trim().toLowerCase()) : undefined);
  const fromHeader = (sources.acceptLanguage ?? "")
    .split(",")
    .map((part, i) => {
      const [tag = "", ...params] = part.split(";");
      const q = Number(params.find((p) => p.trim().startsWith("q="))?.trim().slice(2) ?? 1);
      return { tag: tag.trim(), q: Number.isFinite(q) ? q : 0, i };
    })
    .filter((x) => x.tag && x.tag !== "*" && x.q > 0)
    .sort((a, b) => b.q - a.q || a.i - b.i)
    .map((x) => pick(x.tag) ?? pick(x.tag.split("-")[0]))
    .find(Boolean);
  return pick(sources.url) ?? pick(sources.cookie) ?? fromHeader ?? DEFAULT_LOCALE;
}

/**
 * Tạo hàm dịch từ bảng chuỗi: thiếu key thì trả chính key; `{tên}` thay bằng giá trị tương ứng.
 *
 * @param messages - Bảng chuỗi của một namespace.
 * @returns Hàm dịch `t(key, values?)`.
 */
export function translator(messages: Record<string, string>): (key: string, values?: Record<string, string | number>) => string {
  return (key, values) => (messages[key] ?? key).replace(/\{(\w+)\}/g, (m, name: string) => (values && name in values ? String(values[name]) : m));
}
