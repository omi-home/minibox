import { expect, test, vi } from "vitest";
import { createLocalizationClient, createMessagesCache, resolveLocale, translator } from "./index";

const ok = (messages: Record<string, string>, etag: string) =>
  new Response(JSON.stringify({ locale: "en", namespace: "shop", messages }), { status: 200, headers: { "content-type": "application/json", etag } });

function setup() {
  const fetchMock = vi.fn<typeof fetch>();
  let t = 0;
  const cache = createMessagesCache(createLocalizationClient("http://localization:8080", { fetch: fetchMock }), { ttlMs: 1000, now: () => t });
  return { fetchMock, cache, tick: (ms: number) => (t += ms) };
}

test("Nạp chuỗi một lần, trong TTL dùng cache; hết TTL gửi If-None-Match, 304 giữ bản cũ", async () => {
  const { fetchMock, cache, tick } = setup();
  fetchMock.mockResolvedValueOnce(ok({ "cart.title": "Cart" }, '"v1"')).mockResolvedValueOnce(new Response(null, { status: 304 }));
  expect(await cache.messages("en", "shop")).toEqual({ "cart.title": "Cart" });
  expect(await cache.messages("en", "shop")).toEqual({ "cart.title": "Cart" });
  expect(fetchMock).toHaveBeenCalledTimes(1);
  expect((fetchMock.mock.calls[0]?.[0] as Request).url).toBe("http://localization:8080/messages/en/shop");
  tick(1001);
  expect(await cache.messages("en", "shop")).toEqual({ "cart.title": "Cart" });
  expect((fetchMock.mock.calls[1]?.[0] as Request).headers.get("if-none-match")).toBe('"v1"');
});

test("MessagesPublished làm mới namespace (mọi locale, vì locale khác có thể rơi về locale vừa xuất bản)", async () => {
  const { fetchMock, cache } = setup();
  fetchMock
    .mockResolvedValueOnce(ok({ a: "1" }, '"v1"'))
    .mockResolvedValueOnce(ok({ a: "x" }, '"x1"'))
    .mockResolvedValueOnce(ok({ a: "2" }, '"v2"'))
    .mockResolvedValueOnce(ok({ a: "x2" }, '"x2"'));
  await cache.messages("en", "shop");
  await cache.messages("ja", "shop");
  cache.onMessagesPublished({ locale: "en", namespace: "shop", keys: ["a"] });
  expect(await cache.messages("en", "shop")).toEqual({ a: "2" });
  expect(await cache.messages("ja", "shop")).toEqual({ a: "x2" });
  expect((fetchMock.mock.calls[2]?.[0] as Request).headers.has("if-none-match")).toBe(false);
});

test("localization lỗi: giữ bản cũ nếu có, chưa có thì trả rỗng (giao diện hiện key)", async () => {
  const { fetchMock, cache, tick } = setup();
  fetchMock.mockRejectedValueOnce(new Error("down"));
  expect(await cache.messages("en", "shop")).toEqual({});
  fetchMock.mockResolvedValueOnce(ok({ a: "1" }, '"v1"')).mockResolvedValueOnce(new Response(null, { status: 503 }));
  tick(1001);
  expect(await cache.messages("en", "shop")).toEqual({ a: "1" });
  tick(1001);
  expect(await cache.messages("en", "shop")).toEqual({ a: "1" });
});

test("Chọn locale: URL → cookie → Accept-Language (theo q, khớp ngôn ngữ gốc) → vi; bỏ locale không bật", () => {
  const enabled = ["vi", "en", "ja"];
  expect(resolveLocale({ url: "en", cookie: "ja", acceptLanguage: "ja" }, enabled)).toBe("en");
  expect(resolveLocale({ url: "fr", cookie: "ja" }, enabled)).toBe("ja");
  expect(resolveLocale({ cookie: "fr", acceptLanguage: "fr;q=1, en-US;q=0.8, ja;q=0.5" }, enabled)).toBe("en");
  expect(resolveLocale({ acceptLanguage: "ja;q=0.9, en;q=0.95" }, enabled)).toBe("en");
  expect(resolveLocale({ acceptLanguage: "EN" }, enabled)).toBe("en");
  expect(resolveLocale({ acceptLanguage: "fr, *" }, enabled)).toBe("vi");
  expect(resolveLocale({}, [])).toBe("vi");
});

test("translator: thiếu key trả key, thay {tên}", () => {
  const t = translator({ hi: "Chào {name}, {n} món" });
  expect(t("hi", { name: "An", n: 2 })).toBe("Chào An, 2 món");
  expect(t("hi")).toBe("Chào {name}, {n} món");
  expect(t("missing")).toBe("missing");
});
