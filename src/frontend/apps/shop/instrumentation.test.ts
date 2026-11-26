import { describe, expect, it, vi } from "vitest";

const registerOTel = vi.fn();
vi.mock("@vercel/otel", () => ({ registerOTel }));

describe("instrumentation shop", () => {
  it("ac224_1 bật OpenTelemetry với service.name và truyền traceparent sang service nội bộ (http) nhưng không ra ngoài (https)", async () => {
    const { register } = await import("./instrumentation");
    register();
    const opts = registerOTel.mock.calls[0]?.[0];
    expect(opts.serviceName).toBe("shop");
    const urls: RegExp[] = opts.instrumentationConfig.fetch.propagateContextUrls;
    expect(urls.some((r) => r.test("http://backend-identity:8080/auth/login"))).toBe(true);
    expect(urls.some((r) => r.test("https://api.bank.example/pay"))).toBe(false);
  });
});
