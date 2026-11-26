// Kiểm workflow CI (spec §9.3, NFR #217): mỗi PR chạy lệnh test chuẩn + e2e trên ubuntu có Docker (Testcontainers), cache NuGet/pnpm.
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { root } from "./run.mjs";

const ci = readFileSync(join(root, ".github", "workflows", "ci.yml"), "utf8");

test("ac217_1 CI chạy trên mọi PR: lệnh test chuẩn (gồm Testcontainers) và e2e smoke trên ubuntu-latest", () => {
  assert.match(ci, /^on:\n(?:  .*\n)*  pull_request:/m);
  assert.match(ci, /runs-on: ubuntu-latest/); // Docker có sẵn cho Testcontainers
  assert.match(ci, /run: node scripts\/test\.mjs/);
  assert.match(ci, /run: node scripts\/e2e\.mjs/);
  assert.ok(ci.indexOf("scripts/test.mjs") < ci.indexOf("scripts/e2e.mjs"), "e2e chạy sau test");
  assert.match(ci, /timeout-minutes: 15/);
});

test("ac217_1 CI ghim toolchain theo repo và cache NuGet/pnpm", () => {
  assert.match(ci, /global-json-file: src\/backend\/global\.json/);
  assert.match(ci, /node-version-file: \.nvmrc/);
  const pnpm = JSON.parse(readFileSync(join(root, "src", "frontend", "package.json"), "utf8")).packageManager.split("@")[1];
  assert.match(ci, new RegExp(`version: ${pnpm.replaceAll(".", "\.")}`));
  assert.match(ci, /path: ~\/\.nuget\/packages/);
  assert.match(ci, /cache: pnpm/);
  assert.match(ci, /cache-dependency-path: src\/frontend\/pnpm-lock\.yaml/);
});
