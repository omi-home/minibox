// @vitest-environment node
// Tương phản WCAG AA của token màu (bộ Rừng sâu) × chế độ (sáng, tối), tính từ giá trị oklch trong theme.css; gồm chữ trên cảnh nền.
import { readFileSync } from "node:fs";
import { expect, test } from "vitest";

const css = readFileSync(new URL("./theme.css", import.meta.url), "utf8");

/** Khối `{…}` ngay sau `head` (đếm ngoặc lồng nhau). */
const block = (head: string) => {
  const start = css.indexOf(head);
  if (start < 0) return "";
  let i = css.indexOf("{", start) + 1;
  const from = i;
  for (let depth = 1; depth > 0; i++) depth += css[i] === "{" ? 1 : css[i] === "}" ? -1 : 0;
  return css.slice(from, i - 1);
};

type Lch = [number, number, number];
const parse = (s: string): Lch => {
  const m = /oklch\(\s*([\d.]+)\s+([\d.]+)\s+([\d.]+)/.exec(s);
  if (!m) throw new Error(`Không đọc được màu: ${s}`);
  return [Number(m[1]), Number(m[2]), Number(m[3])];
};

/** Bảng token → [sáng, tối] của một khối CSS. */
const tokens = (src: string) => {
  const out = new Map<string, [Lch, Lch]>();
  for (const m of src.matchAll(/--(?:color-)?((?:scene-)?[\w-]+):\s*light-dark\((oklch\([^)]*\)),\s*(oklch\([^)]*\))\)/g)) out.set(m[1]!, [parse(m[2]!), parse(m[3]!)]);
  return out;
};

// Token màu ở @theme; màu cảnh nền (--scene-*) ở :root của @layer base.
const map = new Map([...tokens(block("@theme static")), ...tokens(block(":root {"))]);

const lum = ([l, c, h]: Lch) => {
  const a = c * Math.cos((h * Math.PI) / 180);
  const b = c * Math.sin((h * Math.PI) / 180);
  const l_ = (l + 0.3963377774 * a + 0.2158037573 * b) ** 3;
  const m_ = (l - 0.1055613458 * a - 0.0638541728 * b) ** 3;
  const s_ = (l - 0.0894841775 * a - 1.291485548 * b) ** 3;
  const rgb = [
    4.0767416621 * l_ - 3.3077115913 * m_ + 0.2309699292 * s_,
    -1.2684380046 * l_ + 2.6097574011 * m_ - 0.3413193965 * s_,
    -0.0041960863 * l_ - 0.7034186147 * m_ + 1.707614701 * s_,
  ].map((v) => Math.min(1, Math.max(0, v)));
  return 0.2126 * rgb[0]! + 0.7152 * rgb[1]! + 0.0722 * rgb[2]!;
};
const ratio = (x: Lch, y: Lch) => {
  const [a, b] = [lum(x), lum(y)];
  return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
};

// [chữ/đồ hoạ, nền, ngưỡng]: chữ thường 4.5, chữ chính 7, viền/focus/đồ hoạ 3.
const pairs: [string, string, number][] = [
  ["foreground", "background", 7],
  ["foreground", "surface", 7],
  ["muted", "surface", 4.5],
  ["muted", "background", 4.5],
  ["muted", "surface-sunken", 4.5],
  ["primary-foreground", "primary", 4.5],
  ["primary-foreground", "primary-hover", 4.5],
  ["accent-foreground", "accent", 4.5],
  ["danger-foreground", "danger", 4.5],
  ["primary", "surface", 3],
  ["focus", "surface", 3],
  ["border-strong", "surface", 1.8],
  ["success", "success-soft", 4.5],
  ["warning", "warning-soft", 4.5],
  ["danger", "danger-soft", 4.5],
  ["info", "info-soft", 4.5],
  ["danger", "surface", 4.5],
  // Chữ đặt thẳng trên nền khi cuộn qua lớp rừng gần nhất (đậm nhất) của cảnh nền.
  ["foreground", "scene-near", 7],
  ["muted", "scene-near", 4.5],
];

for (const [mode, i] of [["sáng", 0], ["tối", 1]] as const)
  for (const [fg, bg, min] of pairs)
    test(`ac231_1 tương phản ${mode}: ${fg} trên ${bg} ≥ ${min}`, () => {
      const f = map.get(fg);
      const b = map.get(bg);
      expect(f, `thiếu --${fg}`).toBeDefined();
      expect(b, `thiếu --${bg}`).toBeDefined();
      expect(ratio(f![i], b![i])).toBeGreaterThanOrEqual(min);
    });
