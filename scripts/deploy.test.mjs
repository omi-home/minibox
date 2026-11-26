// Compose staging/production + Caddy chung (spec §8.2, §8.4). Cần Docker (docker compose config, caddy validate).
import { test } from "node:test";
import assert from "node:assert/strict";
import { execFileSync, spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { composeArgs } from "./dev.mjs";
import { root } from "./run.mjs";

const run = (cmd, args) => execFileSync(cmd, args, { cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] });
const config = (env) => JSON.parse(run("docker", [...composeArgs(env), "config", "--format", "json"]));
const caddyArgs = ["compose", "-f", join("deploy", "caddy", "compose.yaml"), "--env-file", join("deploy", "caddy", ".env.example")];
const ENVS = ["staging", "production"];

for (const env of ENVS) {
  test(`ac220_4 ${env}: secret mount từ /srv/minibox/${env}/secrets/, không có mật khẩu dạng biến môi trường`, () => {
    const c = config(env);
    assert.equal(c.name, `minibox-${env}`);
    const secrets = Object.values(c.secrets ?? {});
    assert.ok(secrets.length >= 3);
    for (const s of secrets) assert.match(s.file.replaceAll("\\", "/"), new RegExp(`^(\\w:)?/srv/minibox/${env}/secrets/[^/]+$`));
    for (const [name, s] of Object.entries(c.services)) {
      assert.equal(s.container_name, `minibox-${env}-${name}`);
      for (const key of Object.keys(s.environment ?? {}))
        assert.ok(!/PASS(WORD)?$/.test(key), `${name}: ${key} phải đọc từ Docker secret (*_FILE)`);
    }
    assert.equal(c.services["database-postgres"].environment.POSTGRES_PASSWORD_FILE, "/run/secrets/postgres_admin");
  });

  test(`ac220_4 ${env}: không dịch vụ nào publish cổng (chỉ Caddy chung ra ngoài)`, () => {
    for (const [name, s] of Object.entries(config(env).services)) assert.equal(s.ports, undefined, `${name} publish cổng`);
  });
}

test("ac220_4 repo không chứa file trong thư mục secrets", () => {
  assert.equal(run("git", ["ls-files", "--", "deploy/*/secrets/*", "**/secrets/*"]).trim(), "");
});

test("ac220_4 dev vẫn chạy được không cần secret ngoài repo (mật khẩu mặc định chỉ ở deploy/dev)", () => {
  const c = config("dev");
  assert.equal(c.services["database-postgres"].environment.POSTGRES_PASSWORD, "minibox");
  assert.doesNotMatch(readFileSync(join(root, "deploy", "compose.yaml"), "utf8"), /PASS(WORD)?:/);
});

test("ac220_4 Caddy chung: nối mạng edge của cả hai môi trường, route theo hostname, staging chỉ qua Tailscale", () => {
  const c = JSON.parse(run("docker", [...caddyArgs, "config", "--format", "json"]));
  const caddy = c.services["infra-caddy"];
  assert.deepEqual(Object.values(c.networks).map((n) => n.name).sort(), ["minibox-production-edge", "minibox-staging-edge"]);
  assert.deepEqual(caddy.ports.map((p) => p.target).sort(), [443, 443, 80]);
  const file = readFileSync(join(root, "deploy", "caddy", "Caddyfile"), "utf8");
  for (const env of ENVS) assert.match(file, new RegExp(`minibox-${env}-frontend-shop:3000`));
  assert.match(file, /\{\$STAGING_SHOP_HOST\} \{\s*import staging/);
  assert.match(file, /not remote_ip 100\.64\.0\.0\/10/);
  const directives = file.replace(/^\s*#.*$/gm, "");
  assert.doesNotMatch(directives, /backend-|\/health|\.well-known/, "chỉ frontend được route ra ngoài");
  for (const env of ENVS)
    assert.match(readFileSync(join(root, "deploy", env, "compose.yaml"), "utf8"), new RegExp(`name: minibox-${env}-edge\\s+external: true`));
});

test("ac220_4 Caddyfile hợp lệ (caddy validate)", () => {
  const caddy = JSON.parse(run("docker", [...caddyArgs, "config", "--format", "json"])).services["infra-caddy"];
  const env = Object.entries(caddy.environment).flatMap(([k, v]) => ["-e", `${k}=${v}`]);
  const r = spawnSync("docker", ["run", "--rm", "-v", `${join(root, "deploy", "caddy")}:/etc/caddy:ro`, ...env, caddy.image, "caddy", "validate", "--config", "/etc/caddy/Caddyfile"], { encoding: "utf8" });
  assert.equal(r.status, 0, r.stderr);
  assert.match(r.stdout + r.stderr, /Valid configuration/);
});
