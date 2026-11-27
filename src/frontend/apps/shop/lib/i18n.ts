import { createLocalizationClient, createMessagesCache, type MessagesCache, resolveLocale, translator } from "@minibox/api-clients";
import { cookies, headers } from "next/headers";

/** Cookie locale khách đã chọn; chỉ dùng để gợi ý đổi ngôn ngữ, không đổi nội dung của URL. */
export const LOCALE_COOKIE = "minibox_locale";

// Gắn vào globalThis: instrumentation.ts (consumer sự kiện) và route được bundle riêng nên biến module không dùng chung.
const shared = globalThis as { miniboxMessages?: MessagesCache };

/**
 * Cache chuỗi giao diện dùng chung trong tiến trình BFF, nạp từ `LOCALIZATION_URL` (mạng nội bộ).
 *
 * @returns Cache; consumer `localization.MessagesPublished` gọi `onMessagesPublished` để làm mới ngay, không chờ TTL.
 * @throws Error khi thiếu `LOCALIZATION_URL`.
 */
export function localization(): MessagesCache {
  if (shared.miniboxMessages) return shared.miniboxMessages;
  const base = process.env.LOCALIZATION_URL;
  if (!base) throw new Error("Thiếu LOCALIZATION_URL");
  return (shared.miniboxMessages = createMessagesCache(createLocalizationClient(base)));
}

/**
 * Locale hiển thị của trang shop: chỉ theo URL (SEO §6.6: một URL một ngôn ngữ), không có tiền tố thì `vi`.
 *
 * @param urlLocale - Tiền tố `/<locale>/` của đường dẫn.
 * @returns Mã locale.
 */
export async function pageLocale(urlLocale?: string): Promise<string> {
  return urlLocale ? resolveLocale({ url: urlLocale }, await localization().locales()) : "vi";
}

/**
 * Locale nên gợi ý cho khách (banner đổi ngôn ngữ): URL → cookie → `Accept-Language` → `vi`.
 *
 * @param urlLocale - Tiền tố `/<locale>/` của đường dẫn.
 * @returns Mã locale gợi ý; khác {@link pageLocale} thì hiện banner, không tự redirect.
 */
export async function suggestedLocale(urlLocale?: string): Promise<string> {
  const [jar, h] = await Promise.all([cookies(), headers()]);
  return resolveLocale(
    { url: urlLocale, cookie: jar.get(LOCALE_COOKIE)?.value, acceptLanguage: h.get("accept-language") ?? undefined },
    await localization().locales(),
  );
}

/**
 * Hàm dịch cho một namespace theo locale của trang.
 *
 * @param locale - Locale của trang ({@link pageLocale}).
 * @param namespace - Nhóm chuỗi, mặc định `shop`.
 * @returns `t(key, values?)`; thiếu chuỗi thì hiện key.
 */
export async function getTranslator(locale: string, namespace = "shop"): Promise<ReturnType<typeof translator>> {
  return translator(await localization().messages(locale, namespace));
}
