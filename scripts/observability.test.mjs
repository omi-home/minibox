// Bộ observability chung (spec §8.5): OTel Collector → Loki/Tempo/Prometheus → Grafana. Cần Docker.
import { test } from "node:test";
import assert from "node:assert/strict";
import { execFileSync, spawnSync } from "node:child_process";
import { randomBytes } from "node:crypto";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { composeArgs } from "./dev.mjs";
import { root } from "./run.mjs";

const run = (cmd, args) => execFileSync(cmd, args, { cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] });
const obsArgs = ["compose", "-f", join("deploy", "observability", "compose.yaml"), "--env-file", join("deploy", "observability", ".env.example")];
const obs = () => JSON.parse(run("docker", [...obsArgs, "config", "--format", "json"]));
const COLLECTOR = "observability-otel-collector";
const NODE = "node:24.22.3-alpine@sha256:116b800c3754b440ee0aa3ed2f20f5bf3d99a554bfd9adeea09ea8017f6e85f5";

test("observability: một bộ chung, chỉ Grafana publish cổng và chỉ trên IP Tailscale", () => {
  const c = obs();
  assert.equal(c.name, "minibox-observability");
  for (const [name, s] of Object.entries(c.services)) {
    assert.equal(s.container_name, `minibox-observability-${name.replace(/^observability-/, "")}`);
    if (name === "observability-grafana") assert.deepEqual(s.ports.map((p) => [p.host_ip, p.target]), [["100.64.0.1", 3000]]);
    else assert.equal(s.ports, undefined, `${name} không được publish cổng (OTLP 4317/4318 chỉ trong mạng Compose)`);
  }
  const col = c.services[COLLECTOR];
  const logs = col.volumes.find((v) => v.target === "/var/lib/docker/containers");
  assert.ok(logs?.read_only, "log container mount chỉ đọc");
  assert.deepEqual(Object.values(c.networks).map((n) => n.name).sort(), ["minibox-observability", "minibox-production-otel", "minibox-staging-otel"]);
  for (const env of ["staging", "production"]) assert.ok(c.networks[env].internal, `mạng OTLP ${env} không ra ngoài`);
});

test("observability: mọi dịch vụ của các môi trường ghi log json-file kèm nhãn project/service cho Collector", () => {
  for (const env of ["dev", "staging", "production"])
    for (const [name, s] of Object.entries(JSON.parse(run("docker", [...composeArgs(env), "config", "--format", "json"])).services)) {
      assert.equal(s.logging?.driver, "json-file", `${env}/${name}`);
      assert.equal(s.logging.options.labels, "com.docker.compose.project,com.docker.compose.service", `${env}/${name}`);
    }
});

test("observability: giới hạn lưu trữ — log 30 ngày, trace 7 ngày, metric 90 ngày/10GB, log Docker xoay vòng", () => {
  const file = (f) => readFileSync(join(root, "deploy", f), "utf8");
  assert.match(file("observability/loki.yaml"), /retention_period: 720h[\s\S]*retention_enabled: true/);
  assert.match(file("observability/tempo.yaml"), /block_retention: 168h/);
  const prom = obs().services["observability-prometheus"].command;
  assert.ok(prom.includes("--storage.tsdb.retention.time=90d") && prom.includes("--storage.tsdb.retention.size=10GB"));
  const logging = JSON.parse(run("docker", [...composeArgs("staging"), "config", "--format", "json"])).services["database-postgres"].logging.options;
  assert.deepEqual([logging["max-size"], logging["max-file"]], ["10m", "5"]);
});

test("observability: cấu hình Collector hợp lệ (otelcol validate)", () => {
  const image = obs().services[COLLECTOR].image;
  const r = spawnSync("docker", ["run", "--rm", "-v", `${join(root, "deploy", "observability", "otel-collector.yaml")}:/etc/otelcol.yaml:ro`, image, "validate", "--config=/etc/otelcol.yaml"], { encoding: "utf8" });
  assert.equal(r.status, 0, r.stdout + r.stderr);
});

// Container giả lập service staging: gửi một span OTLP rồi ghi log JSON (như MiniboxJsonFormatter) cùng trace_id ra stdout.
const emitter = (traceId, spanId) => `
const now = BigInt(Date.now()) * 1000000n;
const attr = (key, v) => ({ key, value: { stringValue: v } });
const body = { resourceSpans: [{ resource: { attributes: [attr("service.name", "probe"), attr("deployment.environment", "staging")] },
  scopeSpans: [{ spans: [{ traceId: "${traceId}", spanId: "${spanId}", name: "GET /probe", kind: 2,
    startTimeUnixNano: String(now - 5000000n), endTimeUnixNano: String(now) }] }] }] };
for (;;) {
  try {
    const r = await fetch("http://${COLLECTOR}:4318/v1/traces", { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify(body) });
    if (r.ok) break;
  } catch {}
  await new Promise((f) => setTimeout(f, 1000));
}
console.log(JSON.stringify({ ts: new Date().toISOString(), level: "Information", category: "Probe", message: "probe request", trace_id: "${traceId}", span_id: "${spanId}" }));
setInterval(() => {}, 1e6);`;

// Chạy trong mạng nội bộ của bộ observability: chờ log trong Loki (nhãn service/env theo container) rồi trace trong Tempo.
const checker = (traceId) => `
const until = Date.now() + 120000;
const poll = async (what, fn) => { for (;;) { try { const v = await fn(); if (v) return v; } catch {} if (Date.now() > until) throw new Error("hết giờ chờ " + what); await new Promise((f) => setTimeout(f, 2000)); } };
const q = encodeURIComponent('{service_name="probe", deployment_environment="staging"} | trace_id="${traceId}"');
const start = (Date.now() - 3600000) * 1000000;
const log = await poll("log Loki", async () => (await (await fetch("http://observability-loki:3100/loki/api/v1/query_range?query=" + q + "&start=" + start)).json()).data.result[0]);
const trace = await poll("trace Tempo", async () => { const r = await fetch("http://observability-tempo:3200/api/traces/${traceId}"); return r.ok && (await r.text()); });
console.log(JSON.stringify({ stream: log.stream, line: log.values[0][1], traceHasService: trace.includes("probe") }));`;

const running = () => run("docker", ["ps", "-q", "--filter", "label=com.docker.compose.project=minibox-observability"]).trim() !== "";

test("ac224_1 log của một request có trace_id trùng trace trong Tempo", { timeout: 600000 }, (t) => {
  if (running()) return t.skip("bộ observability đang chạy trên máy này — không dựng/xoá đè");
  const traceId = randomBytes(16).toString("hex");
  const spanId = randomBytes(8).toString("hex");
  const name = "minibox-staging-backend-probe";
  try {
    run("docker", [...obsArgs, "up", "-d", "--wait", COLLECTOR, "observability-loki", "observability-tempo"]);
    spawnSync("docker", ["rm", "-f", name]);
    run("docker", ["run", "-d", "--name", name, "--network", "minibox-staging-otel",
      "--label", "com.docker.compose.project=minibox-staging", "--label", "com.docker.compose.service=backend-probe",
      "--log-driver", "json-file", "--log-opt", "labels=com.docker.compose.project,com.docker.compose.service",
      NODE, "node", "--input-type=module", "-e", emitter(traceId, spanId)]);
    const out = JSON.parse(run("docker", ["run", "--rm", "--network", "minibox-observability", NODE, "node", "--input-type=module", "-e", checker(traceId)]).trim().split("\n").at(-1));
    assert.equal(out.stream.service_name, "probe");
    assert.equal(out.stream.deployment_environment, "staging");
    assert.equal(out.line, "probe request");
    assert.ok(out.traceHasService, "trace trong Tempo thuộc service probe");
  } finally {
    spawnSync("docker", ["rm", "-f", name]);
    spawnSync("docker", [...obsArgs, "down", "-v"]);
  }
});
