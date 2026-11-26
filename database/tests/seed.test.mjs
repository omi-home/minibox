// Seed demo chỉ chạy ở dev/staging (spec §4.6, BRD §18). Từ chối xảy ra trước khi chạm Docker nên không cần DB.
import { test } from "node:test";
import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const script = fileURLToPath(new URL("../seed/run.mjs", import.meta.url));
const seed = (env, args = []) =>
  spawnSync("node", [script, ...args], { encoding: "utf8", env: { ...process.env, ENVIRONMENT: env } });

test("ac180_5 seed demo từ chối chạy khi ENVIRONMENT=production", () => {
  const r = seed("production");
  assert.notEqual(r.status, 0);
  assert.match(r.stderr, /production/);
});

test("ac180_5 tham số env=production cũng bị từ chối dù ENVIRONMENT=dev", () => {
  assert.notEqual(seed("dev", ["production"]).status, 0);
});

test("seed demo từ chối ENVIRONMENT thiếu hoặc lạ", () => {
  assert.notEqual(seed("").status, 0);
  assert.notEqual(seed("prod").status, 0);
});
