// @vitest-environment node
import { readFileSync } from "node:fs";
import { compile } from "tailwindcss";
import { expect, test } from "vitest";

const theme = readFileSync(new URL("./theme.css", import.meta.url), "utf8");
const build = async (classes: string[]) => (await compile(`${theme}\n@tailwind utilities;`)).build(classes);

test("ac231_1 token CSS variables đủ 5 nhóm (màu, chữ, khoảng cách, bo góc, độ sâu) trên :root", async () => {
  const css = await build([]);
  for (const v of ["--color-primary", "--color-accent", "--color-danger", "--color-focus", "--color-background", "--color-foreground", "--color-surface-raised", "--font-display", "--font-sans", "--font-mono", "--spacing", "--spacing-box", "--radius-box", "--chamfer", "--shadow-box-1", "--shadow-box-3", "--shadow-plinth", "--container-prose"])
    expect(css).toMatch(new RegExp(`${v}:`));
});

test("ac231_1 utility sinh từ token; breakpoint đúng 4 nhóm thiết bị 640/1024/1536, không còn md/xl", async () => {
  const css = await build(["bg-primary", "text-muted", "rounded-box", "shadow-box-2", "p-box", "max-w-prose", "sm:flex", "lg:flex", "2xl:flex", "md:flex", "xl:flex"]);
  for (const c of ["bg-primary", "text-muted", "rounded-box", "shadow-box-2", "p-box", "max-w-prose"]) expect(css).toContain(`.${c} {`);
  expect(css).toContain("max-width: var(--container-prose)");
  expect([...css.matchAll(/@media \(width >= ([\d.]+rem)\)/g)].map((m) => m[1])).toEqual(["40rem", "64rem", "96rem"]);
  expect(css).not.toMatch(/\.md\:flex|\.xl\:flex/);
});

test("ac231_2 theme.css khai @source để app sinh utility cho class trong component của packages/ui", () => {
  expect(theme).toMatch(/@source "\.\/";/);
});

test("ac231_1 màu khai light-dark (sáng/tối); data-theme ép chế độ; không còn token cạnh đáy kiểu phím bấm", () => {
  expect(theme).toMatch(/--color-background: light-dark\(/);
  expect(theme).toMatch(/\[data-theme="dark"\] \{\s*color-scheme: dark;/);
  expect(theme).not.toMatch(/--color-edge|shadow-key|--key-edge/);
});

test("ac231_1 góc vát nắp hộp và lớp xếp chồng sinh thành utility", async () => {
  const css = await build(["chamfer", "chamfer-sm", "z-modal", "z-toast", "font-display", "shadow-plinth"]);
  expect(css).toMatch(/\.chamfer \{[^}]*corner-top-right-shape: bevel/);
  expect(css).toContain("z-index: var(--z-index-modal)");
  expect(css).toContain("font-family: var(--font-display)");
});
