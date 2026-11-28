import {
  createLocalizationClient,
  createMessagesCache,
  type LocalizationClient,
  type MessagesCache,
  resolveLocale,
  translator,
} from "@minibox/api-clients";
import { cookies, headers } from "next/headers";

const codes = (list: { code: string }[]) => list.map((l) => l.code);
import { LOCALE_COOKIE, THEME_COOKIE, themeOf } from "./menu";
import { SESSION_COOKIE, unsealSession } from "./session";

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
  // LOCALIZATION_CACHE_TTL_MS: e2e đặt 1 để thấy ngay ngôn ngữ/chuỗi vừa đổi (không có RabbitMQ báo xuất bản).
  const ttlMs = Number(process.env.LOCALIZATION_CACHE_TTL_MS) || undefined;
  return (shared.miniboxMessages = createMessagesCache(createLocalizationClient(base), { ttlMs }));
}

/**
 * Client localization gọi API quản trị (`/admin/*`) bằng access token của phiên hiện tại; API tự kiểm quyền.
 *
 * @returns Client có kiểu; request ghi tự kèm `Idempotency-Key`.
 * @throws Error khi thiếu `LOCALIZATION_URL`.
 */
export async function localizationAdmin(): Promise<LocalizationClient> {
  const base = process.env.LOCALIZATION_URL;
  if (!base) throw new Error("Thiếu LOCALIZATION_URL");
  const token = unsealSession((await cookies()).get(SESSION_COOKIE)?.value)?.accessToken;
  return createLocalizationClient(base, token ? { headers: { authorization: `Bearer ${token}` } } : {});
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
    codes(await localization().locales()),
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

/**
 * Lựa chọn hiển thị cho trang ngoài khung admin (đăng nhập, MFA): giao diện từ cookie, ngôn ngữ của request và các ngôn ngữ đang bật.
 *
 * @returns `theme`, `locale`, `locales` để truyền cho nút đổi giao diện và menu ngôn ngữ.
 */
export async function displayPreferences() {
  const [jar, locale, locales] = await Promise.all([cookies(), requestLocale(), localization().locales()]);
  return { theme: themeOf(jar.get(THEME_COOKIE)?.value), locale, locales: locales.map(({ code, name }) => ({ code, name })) };
}
/**
 * Chuỗi màn xác thực (`auth.*`, namespace `admin`) đã xuất bản của ngôn ngữ request, để client dịch đăng nhập, MFA và xác thực lại.
 *
 * @returns Bảng key → chuỗi; key thiếu thì client dùng bản tiếng Việt mặc định.
 */
export async function authMessages(): Promise<Record<string, string>> {
  const all = await localization().messages(await requestLocale(), "admin");
  return Object.fromEntries(Object.entries(all).filter(([key]) => key.startsWith("auth.")));
}