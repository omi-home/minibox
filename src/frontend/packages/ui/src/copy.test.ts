// @vitest-environment node
// Giọng văn và kỷ luật token (spec #448 §5, §8): quét mã giao diện của packages/ui và apps/*/app.
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join } from "node:path";
import { expect, test } from "vitest";

const frontend = new URL("../../../", import.meta.url).pathname.replace(/^\/(\w:)/, "$1");

/** Mọi file .tsx (bỏ test) dưới `dir`. */
const tsx = (dir: string): string[] =>
  readdirSync(dir).flatMap((f) => {
    const p = join(dir, f);
    if (f === "node_modules" || f.startsWith(".")) return [];
    if (statSync(p).isDirectory()) return tsx(p);
    return p.endsWith(".tsx") && !p.includes(".test.") ? [p] : [];
  });

const uiFiles = tsx(join(frontend, "packages/ui/src"));
const appFiles = ["shop", "admin"].flatMap((a) => tsx(join(frontend, "apps", a, "app")));
/** Nội dung file đã bỏ comment (comment được phép dùng gạch dài, mã màu khi giải thích). */
const code = (p: string) => readFileSync(p, "utf8").replace(/\/\*[\s\S]*?\*\/|\{\/\*[\s\S]*?\*\/\}|(^|[^:])\/\/.*$/gm, "$1");

test("ac231_2 quét được mã giao diện", () => {
  expect(uiFiles.length).toBeGreaterThan(5);
  expect(appFiles.length).toBeGreaterThan(1);
});

test("ac231_2 chuỗi giao diện không có gạch dài và từ sáo", () => {
  const bad = [...uiFiles, ...appFiles].flatMap((p) => {
    const hits = code(p).match(/[—–]|nâng tầm|liền mạch|đột phá|trải nghiệm tuyệt vời|tối ưu hoá trải nghiệm/gi);
    return hits ? [`${p.slice(frontend.length)}: ${[...new Set(hits)].join(", ")}`] : [];
  });
  expect(bad).toEqual([]);
});

test("ac231_2 app chỉ dùng token màu, không mã màu thô hay bảng màu mặc định của Tailwind", () => {
  const raw = /#[0-9a-f]{3,8}\b|\brgba?\(|\boklch\(|\b(?:bg|text|border|fill|stroke|ring)-(?:slate|gray|zinc|neutral|stone|red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|pink|rose)-\d{2,3}\b/gi;
  const bad = appFiles.flatMap((p) => {
    const hits = code(p).match(raw);
    return hits ? [`${p.slice(frontend.length)}: ${[...new Set(hits)].join(", ")}`] : [];
  });
  expect(bad).toEqual([]);
});
