// Chạy sau build backend (contracts/openapi vừa sinh lại). Cần Docker cho oasdiff.
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { root } from "../../scripts/run.mjs";
import { breakingErrors, checkAgainst } from "../breaking.mjs";

const identity = () => JSON.parse(readFileSync(join(root, "contracts", "openapi", "identity.json"), "utf8"));
const withoutLastPath = () => {
  const s = identity();
  delete s.paths[Object.keys(s.paths).pop()];
  return s;
};

test("ac212_2 xoá endpoint mà không tăng phiên bản bị báo phá vỡ", () => {
  const errors = breakingErrors(identity(), withoutLastPath());
  assert.equal(errors.length, 1);
  assert.match(errors[0], /api-path-removed/);
});

test("ac212_2 xoá endpoint kèm tăng major info.version thì được phép", () => {
  const head = withoutLastPath();
  head.info.version = `${Number.parseInt(head.info.version, 10) + 1}.0.0`;
  assert.deepEqual(breakingErrors(identity(), head), []);
});

test("ac212_2 thêm endpoint mới không phá vỡ", () => {
  const head = identity();
  head.paths["/ac212-new"] = { get: { responses: { 200: { description: "OK" } } } };
  assert.deepEqual(breakingErrors(identity(), head), []);
});

test("ac212_2 OpenAPI hiện tại không phá vỡ so với develop (hoặc đã tăng major)", () => {
  assert.deepEqual(checkAgainst(), {});
});
