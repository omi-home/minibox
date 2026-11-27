// @vitest-environment node
import { readFileSync } from "node:fs";
import { compile } from "tailwindcss";
import { expect, test } from "vitest";

const theme = readFileSync(new URL("./theme.css", import.meta.url), "utf8");
const build = async (classes: string[]) => (await compile(`${theme}\n@tailwind utilities;`)).build(classes);

test("ac231_1 token CSS variables đủ 5 nhóm (màu, chữ, khoảng cách, bo góc, độ sâu) trên :root", async () => {
  const css = await build([]);
  for (const v of ["--color-primary", "--color-accent", "--color-danger", "--color-focus", "--color-background", "--color-foreground", "--font-sans", "--spacing", "--spacing-box", "--radius-box", "--shadow-box-1", "--shadow-box-3", "--container-prose"])
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
