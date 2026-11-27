import { createLocalizationClient, createMessagesCache, type MessagesCache, resolveLocale, translator } from "@minibox/api-clients";
import { cookies, headers } from "next/headers";
import { LOCALE_COOKIE } from "./menu";

let cache: MessagesCache | undefined;

/**
 * Cache chuỗi giao diện dùng chung trong tiến trình BFF, nạp từ `LOCALIZATION_URL` (mạng nội bộ).
 *
 * @returns Cache; consumer `localization.MessagesPublished` gọi `onMessagesPublished` để làm mới ngay, không chờ TTL.
 * @throws Error khi thiếu `LOCALIZATION_URL`.
 */
export function localization(): MessagesCache {
  if (cache) return cache;
  const base = process.env.LOCALIZATION_URL;
  if (!base) throw new Error("Thiếu LOCALIZATION_URL");
  return (cache = createMessagesCache(createLocalizationClient(base)));
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
