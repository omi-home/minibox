import { createLocalizationClient, createMessagesCache, type MessagesCache, resolveLocale, translator } from "@minibox/api-clients";
import { cookies, headers } from "next/headers";
import { LOCALE_COOKIE } from "./menu";

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
 * Locale của request admin: cookie → `Accept-Language` → `vi` (admin không có locale trên URL), chỉ nhận locale đang bật.
 *
 * @returns Mã locale.
 */
export async function requestLocale(): Promise<string> {
  const [jar, h] = await Promise.all([cookies(), headers()]);
  return resolveLocale(
    { cookie: jar.get(LOCALE_COOKIE)?.value, acceptLanguage: h.get("accept-language") ?? undefined },
    await localization().locales(),
  );
}

/**
 * Hàm dịch cho một namespace theo locale của request.
 *
 * @param namespace - Nhóm chuỗi, mặc định `admin`.
 * @returns `t(key, values?)`; thiếu chuỗi thì hiện key.
 */
export async function getTranslator(namespace = "admin"): Promise<ReturnType<typeof translator>> {
  return translator(await localization().messages(await requestLocale(), namespace));
}
