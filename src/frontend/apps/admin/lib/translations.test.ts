import { expect, test } from "vitest";
import { type AdminMessage, buildKeys, summarize } from "./translations";

const msg = (key: string, locale: string, published: boolean, draft = `${key}-${locale}`): AdminMessage => ({
  id: `${key}-${locale}`,
  namespace: "admin",
  key,
  locale,
  draftText: draft,
  publishedText: published ? draft : null,
  publishedAt: null,
  updatedAt: "2026-11-28T00:00:00Z",
  version: 1,
});

const rows = [msg("save", "vi", true), msg("save", "en", false), msg("cancel", "vi", true), msg("title", "en", true)];

test("buildKeys gom theo key, sắp theo key, lọc key thiếu ở một locale", () => {
  const all = buildKeys(rows, {});
  expect(all.map((k) => k.key)).toEqual(["cancel", "save", "title"]);
  expect(all[1]!.byLocale.en?.draftText).toBe("save-en");
  expect(buildKeys(rows, { missing: "en" }).map((k) => k.key)).toEqual(["cancel"]);
  expect(buildKeys(rows, { missing: "vi" }).map((k) => k.key)).toEqual(["title"]);
});

test("buildKeys tìm không phân biệt hoa thường theo key và nội dung", () => {
  expect(buildKeys(rows, { q: "SAVE" }).map((k) => k.key)).toEqual(["save"]);
  expect(buildKeys(rows, { q: "title-en" }).map((k) => k.key)).toEqual(["title"]);
});

test("summarize đếm key thiếu và nháp chưa xuất bản theo locale", () => {
  expect(summarize(rows, ["vi", "en", "ja"])).toEqual({
    vi: { missing: 1, unpublished: 0 },
    en: { missing: 1, unpublished: 1 },
    ja: { missing: 3, unpublished: 0 },
  });
});

test("summarize: nháp sửa khác bản đã xuất bản tính là chưa xuất bản", () => {
  const edited = { ...msg("save", "vi", true), draftText: "Lưu lại" };
  expect(summarize([edited], ["vi"]).vi).toEqual({
    missing: 0,
    unpublished: 1,
  });
});
