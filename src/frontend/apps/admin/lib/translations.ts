import type { LocalizationComponents } from "@minibox/api-clients";

/** Chuỗi giao diện ở màn quản trị (`GET /admin/messages`). */
export type AdminMessage = LocalizationComponents["schemas"]["UiMessageDto"];

/** Locale ở màn quản trị (`GET /admin/locales`). */
export type AdminLocale = LocalizationComponents["schemas"]["LocaleAdminDto"];

/** Tiêu đề màn bản dịch (dùng chung với trang lỗi phía server). */
export const TRANSLATIONS_TITLE = "Bản dịch giao diện";

/** Mô tả màn bản dịch. */
export const TRANSLATIONS_DESCRIPTION = "Sửa chuỗi của trang quản trị và cửa hàng, xuất bản theo từng ngôn ngữ.";

/** Tiêu đề màn ngôn ngữ (dùng chung với trang lỗi phía server). */
export const LOCALES_TITLE = "Ngôn ngữ";

/** Mô tả màn ngôn ngữ. */
export const LOCALES_DESCRIPTION = "Ngôn ngữ người dùng chọn được ở cửa hàng và trang quản trị. Tiếng Việt luôn bật và là dự phòng cuối.";

/**
 * Nhóm chuỗi admin quản lý được: mỗi BFF nạp một namespace.
 *
 * @remarks ponytail: danh sách cố định; thêm endpoint liệt kê namespace ở localization khi có namespace thứ ba.
 */
export const NAMESPACES = [
  { value: "admin", label: "Trang quản trị" },
  { value: "shop", label: "Cửa hàng" },
] as const;

/** Một key với bản dịch theo từng locale. */
export type KeyRow = {
  key: string;
  byLocale: Record<string, AdminMessage | undefined>;
};

/** Đã có bản dịch nhưng nháp chưa xuất bản (mới hoặc đã sửa sau lần xuất bản gần nhất). */
export const isUnpublished = (m: AdminMessage) => m.publishedText !== m.draftText;

/**
 * Gom chuỗi của một namespace thành dòng theo key, sắp theo key.
 *
 * @param messages - Chuỗi mọi locale của namespace.
 * @param filter - `missing`: chỉ giữ key chưa có bản dịch ở locale này; `q`: tìm theo key hoặc nội dung (không phân biệt hoa thường).
 * @returns Dòng theo key.
 */
export function buildKeys(messages: AdminMessage[], filter: { missing?: string; q?: string }): KeyRow[] {
  const map = new Map<string, KeyRow>();
  for (const m of messages) {
    const row = map.get(m.key) ?? { key: m.key, byLocale: {} };
    row.byLocale[m.locale] = m;
    map.set(m.key, row);
  }
  const q = filter.q?.trim().toLowerCase();
  return [...map.values()]
    .filter((r) => !filter.missing || !r.byLocale[filter.missing])
    .filter(
      (r) =>
        !q ||
        r.key.toLowerCase().includes(q) ||
        Object.values(r.byLocale).some((m) => m?.draftText.toLowerCase().includes(q) || m?.publishedText?.toLowerCase().includes(q)),
    )
    .sort((a, b) => a.key.localeCompare(b.key));
}

/**
 * Đếm theo locale: số key của namespace chưa có bản dịch, số bản dịch có nháp chưa xuất bản.
 *
 * @param messages - Chuỗi mọi locale của namespace.
 * @param locales - Mã locale cần đếm.
 * @returns `{ [locale]: { missing, unpublished } }`.
 */
export function summarize(messages: AdminMessage[], locales: string[]): Record<string, { missing: number; unpublished: number }> {
  const keys = new Set(messages.map((m) => m.key));
  return Object.fromEntries(
    locales.map((l) => {
      const own = messages.filter((m) => m.locale === l);
      return [
        l,
        {
          missing: keys.size - new Set(own.map((m) => m.key)).size,
          unpublished: own.filter(isUnpublished).length,
        },
      ];
    }),
  );
}
