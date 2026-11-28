import { test } from "node:test";
import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { composeArgs } from "./dev.mjs";
import { root } from "./run.mjs";

const config = (env) =>
  JSON.parse(execFileSync("docker", [...composeArgs(env), "config", "--format", "json"], { cwd: root, encoding: "utf8" }));

const INFRA = ["database-postgres", "infra-rabbitmq", "infra-seaweedfs", "infra-clamav", "infra-mailpit"];

test("ac174_4 compose dev gom thành cụm minibox-dev, đủ 5 dịch vụ hạ tầng có health check", () => {
  const c = config("dev");
  assert.equal(c.name, "minibox-dev");
  for (const svc of INFRA) {
    const s = c.services[svc];
    assert.ok(s, `thiếu ${svc}`);
    assert.equal(s.container_name, `minibox-dev-${svc}`);
    assert.ok(s.healthcheck?.test, `${svc} thiếu healthcheck`);
  }
});

test("ac174_4 tên dịch vụ theo layer <layer>-<tên>", () => {
  for (const svc of Object.keys(config("dev").services))
    assert.match(svc, /^(frontend|backend|database|infra|observability)-[a-z0-9-]+$/);
});

test("compose dev tạo bucket catalog-files/catalog-preview/catalog-evidence khi SeaweedFS khởi động", () => {
  const hook = config("dev").services["infra-seaweedfs"].post_start?.map((h) => h.command.join(" ")).join("\n") ?? "";
  for (const b of ["catalog-files", "catalog-preview", "catalog-evidence"]) assert.ok(hook.includes(` ${b}`), `thiếu bucket ${b}`);
});
