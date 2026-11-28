// Identity giả cho e2e admin: cùng hợp đồng HTTP với contracts/openapi/identity.json (/auth/*), dữ liệu trong bộ nhớ.
// ponytail: thay bằng identity thật khi e2e chạy trên stack Compose (#219); hành vi thật đã có AuthApiTests ac208_1 phía .NET.
import { randomBytes, randomUUID } from "node:crypto";
import { createServer } from "node:http";
import { base32, totp } from "./totp.mjs";

const PASSWORD = "Mat-khau-du-dai-1";
// `moi` chưa bật MFA; `cu` đã bật với secret cố định (e2e tự tính mã TOTP).
const accounts = {
  "moi@minibox.vn": { id: randomUUID(), secret: undefined, confirmed: false, lastStep: -1, recovery: [] },
  "cu@minibox.vn": { id: randomUUID(), secret: "JBSWY3DPEHPK3PXP", confirmed: true, lastStep: -1, recovery: [] },
};
const access = new Map(); // access token → tài khoản
const refresh = new Map(); // refresh token → { a, amr }

const matchStep = (a, code) => {
  const now = Math.floor(Date.now() / 30_000);
  return [now - 1, now, now + 1].find((s) => s > a.lastStep && totp(a.secret, s) === code);
};

const tokens = (a, amr) => {
  const at = randomBytes(16).toString("hex");
  const rt = randomBytes(16).toString("hex");
  access.set(at, a);
  refresh.set(rt, { a, amr });
  return {
    accessToken: at,
    accessTokenExpiresAt: new Date(Date.now() + 300_000).toISOString(),
    refreshToken: rt,
    refreshTokenExpiresAt: new Date(Date.now() + 3_600_000).toISOString(),
    authMethods: amr,
  };
};

const routes = {
  "/auth/login": (b) => {
    const a = accounts[String(b.identifier).toLowerCase()];
    return a && b.password === PASSWORD ? [200, tokens(a, ["pwd"])] : [400, { code: "Identity.InvalidCredentials" }];
  },
  "/auth/mfa/enrollment": (_, a) => {
    if (a.confirmed) return [400, { code: "Identity.MfaAlreadyEnrolled" }];
    a.secret = base32(randomBytes(20));
    return [200, { secret: a.secret, otpauthUri: `otpauth://totp/Minibox:x?secret=${a.secret}&issuer=Minibox` }];
  },
  "/auth/mfa/enrollment/confirm": (b, a) => {
    if (!a.secret || a.confirmed) return [400, { code: "Identity.MfaNotEnrolled" }];
    const step = matchStep(a, b.code);
    if (step === undefined) return [400, { code: "Identity.InvalidMfaCode" }];
    Object.assign(a, { confirmed: true, lastStep: step, recovery: Array.from({ length: 10 }, () => randomBytes(5).toString("hex")) });
    return [200, [...a.recovery]];
  },
  "/auth/mfa/verify": (b, a) => {
    if (!a.confirmed) return [400, { code: "Identity.MfaNotEnrolled" }];
    const step = matchStep(a, b.code);
    const r = a.recovery.indexOf(b.code);
    if (step === undefined && r < 0) return [400, { code: "Identity.InvalidMfaCode" }];
    if (step !== undefined) a.lastStep = step;
    else a.recovery.splice(r, 1);
    refresh.delete(b.refreshToken);
    return [200, tokens(a, ["pwd", "mfa"])];
  },
  "/auth/refresh": (b) => {
    const s = refresh.get(b.refreshToken);
    if (!s) return [401, { code: "Identity.InvalidRefreshToken" }];
    refresh.delete(b.refreshToken);
    return [200, tokens(s.a, s.amr)];
  },
  "/auth/logout": (b) => {
    refresh.delete(b.refreshToken);
    return [204];
  },
};
const needsBearer = new Set(["/auth/mfa/enrollment", "/auth/mfa/enrollment/confirm", "/auth/mfa/verify"]);

createServer(async (req, res) => {
  let raw = "";
  for await (const chunk of req) raw += chunk;
  const route = req.method === "POST" && routes[req.url];
  const a = access.get(req.headers.authorization?.replace(/^Bearer /, ""));
  const [status, body] = !route
    ? [404, { code: "not_found" }]
    : !req.headers["idempotency-key"]
      ? [400, { code: "Idempotency.InvalidKey" }]
      : needsBearer.has(req.url) && !a
        ? [401, { code: "unauthenticated" }]
        : route(raw ? JSON.parse(raw) : {}, a);
  res.writeHead(status, body ? { "content-type": "application/json" } : {}).end(body ? JSON.stringify(body) : undefined);
}).listen(Number(process.env.PORT ?? 5199));
