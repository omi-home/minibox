// Stub service localization cho e2e: giữ trạng thái trong bộ nhớ, theo hợp đồng contracts/openapi/localization.json
// (locale, chuỗi nháp → xuất bản, chuỗi fallback). Chạy bởi webServer trong playwright.config.ts.
// ponytail: không kiểm JWT/quyền (API thật tự kiểm, có test riêng ở backend); chỉ đòi có header Authorization ở /admin.
import http from "node:http";

const port = Number(process.argv[2] ?? 3102);
const now = () => new Date().toISOString();
/** @type {{ code: string, name: string, enabled: boolean, sortOrder: number, fallbackCode: string | null, version: number }[]} */
let locales;
/** @type {{ id: string, namespace: string, key: string, locale: string, draftText: string, publishedText: string | null, publishedAt: string | null, updatedAt: string, version: number }[]} */
let messages;

/** Dữ liệu khởi đầu như seed thật: `vi` + `en`; `en` cố ý thiếu vài key để thấy fallback về `vi`. */
const reset = () => {
  locales = [
    { code: "vi", name: "Tiếng Việt", enabled: true, sortOrder: 10, fallbackCode: null, version: 1 },
    { code: "en", name: "English", enabled: true, sortOrder: 20, fallbackCode: null, version: 1 },
  ];
  messages = [];
  const seed = {
    admin: {
      vi: { "dashboard.title": "Tổng quan", "dashboard.description": "Việc cần xử lý hôm nay theo quyền của bạn." },
      en: { "dashboard.title": "Dashboard" },
    },
    shop: {
      vi: { "home.title": "Chào mừng đến Minibox", "home.tagline": "Đồ in 3D theo yêu cầu", "home.languages": "Ngôn ngữ" },
      en: { "home.title": "Welcome to Minibox", "home.languages": "Languages" },
    },
  };
  for (const [namespace, byLocale] of Object.entries(seed))
    for (const [locale, texts] of Object.entries(byLocale)) for (const [key, text] of Object.entries(texts)) save(namespace, key, locale, text, true);
};

const save = (namespace, key, locale, text, publish = false) => {
  const m = messages.find((x) => x.namespace === namespace && x.key === key && x.locale === locale);
  if (m) Object.assign(m, { draftText: text, updatedAt: now(), version: m.version + 1 });
  else messages.push({ id: crypto.randomUUID(), namespace, key, locale, draftText: text, publishedText: null, publishedAt: null, updatedAt: now(), version: 1 });
  if (publish) publishOne(messages.find((x) => x.namespace === namespace && x.key === key && x.locale === locale));
};
const publishOne = (m) => Object.assign(m, { publishedText: m.draftText, publishedAt: now() });

/** Chuỗi fallback như LocaleFallback.Chain: locale → fallback → … → `vi`. */
const chain = (code) => {
  const out = [];
  for (let c = code; c && !out.includes(c); c = locales.find((l) => l.code === c)?.fallbackCode ?? (c === "vi" ? null : "vi")) out.push(c);
  return out;
};

const send = (res, status, body) => {
  res.writeHead(status, { "content-type": "application/json" });
  res.end(body === undefined ? "" : JSON.stringify(body));
};

reset();
http
  .createServer(async (req, res) => {
    let raw = "";
    for await (const c of req) raw += c;
    const body = raw ? JSON.parse(raw) : {};
    const url = new URL(req.url ?? "/", "http://stub");
    const p = url.pathname.split("/").slice(1).map(decodeURIComponent);
    const m = req.method;
    if (url.pathname === "/__reset") return reset(), send(res, 204);
    if (url.pathname === "/locales")
      return send(res, 200, locales.filter((l) => l.enabled).sort((a, b) => a.sortOrder - b.sortOrder).map(({ code, name, fallbackCode }) => ({ code, name, fallbackCode })));
    if (p[0] === "messages" && p.length === 3) {
      if (!locales.some((l) => l.code === p[1] && l.enabled)) return send(res, 404, { title: "Không có ngôn ngữ" });
      const order = chain(p[1]).reverse(); // vi trước, locale của trang ghi đè sau cùng
      const out = {};
      for (const code of order) for (const x of messages) if (x.namespace === p[2] && x.locale === code && x.publishedText !== null) out[x.key] = x.publishedText;
      return send(res, 200, { locale: p[1], namespace: p[2], messages: out });
    }
    if (p[0] !== "admin") return send(res, 404, {});
    if (!req.headers.authorization) return send(res, 401, { title: "Chưa đăng nhập" });
    if (p[1] === "locales" && p.length === 2 && m === "GET") return send(res, 200, locales);
    if (p[1] === "locales" && p.length === 2 && m === "POST") {
      if (locales.some((l) => l.code === body.code)) return send(res, 409, { title: "Trùng mã", detail: "Ngôn ngữ đã tồn tại." });
      locales.push({ code: body.code, name: body.name, enabled: false, sortOrder: body.sortOrder, fallbackCode: body.fallbackCode ?? null, version: 1 });
      return send(res, 204);
    }
    if (p[1] === "locales" && p.length === 3 && m === "PUT") {
      const l = locales.find((x) => x.code === p[2]);
      if (!l) return send(res, 404, {});
      if (Number(body.version) !== l.version) return send(res, 409, { title: "Xung đột", detail: "Dữ liệu đã bị người khác sửa." });
      Object.assign(l, { name: body.name, enabled: body.enabled, sortOrder: body.sortOrder, fallbackCode: body.fallbackCode ?? null, version: l.version + 1 });
      return send(res, 204);
    }
    if (p[1] === "messages" && p.length === 2) return send(res, 200, messages.filter((x) => x.namespace === url.searchParams.get("namespace")));
    if (p[1] === "messages" && p.length === 5 && m === "PUT") return save(p[2], p[3], p[4], body.text), send(res, 204);
    if (p[1] === "messages" && p[4] === "publish" && m === "POST") {
      const pending = messages.filter((x) => x.namespace === p[2] && x.locale === p[3] && x.publishedText !== x.draftText);
      pending.forEach(publishOne);
      return send(res, 200, { published: pending.length });
    }
    send(res, 404, {});
  })
  .listen(port, "127.0.0.1", () => console.log(`localization stub http://127.0.0.1:${port}`));
