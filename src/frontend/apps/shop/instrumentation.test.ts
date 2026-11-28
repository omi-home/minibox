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

const consumeMessagesPublished = vi.fn(async () => async () => {});
const onMessagesPublished = vi.fn();
vi.mock("@minibox/api-clients/events", () => ({ consumeMessagesPublished }));
vi.mock("./lib/i18n", () => ({ localization: () => ({ onMessagesPublished }) }));

describe("instrumentation: sự kiện localization", () => {
  it("Có RABBITMQ_URL (runtime nodejs) thì nghe MessagesPublished và làm mới cache chuỗi", async () => {
    vi.stubEnv("NEXT_RUNTIME", "nodejs");
    vi.stubEnv("RABBITMQ_URL", "amqp://mq");
    const { register } = await import("./instrumentation");
    await register();
    expect(consumeMessagesPublished).toHaveBeenCalledWith("amqp://mq", expect.any(Function));
    const event = { locale: "en", namespace: "x", keys: ["a"] };
    (consumeMessagesPublished.mock.calls[0] as unknown as [string, (e: typeof event) => void])[1](event);
    expect(onMessagesPublished).toHaveBeenCalledWith(event);
    vi.unstubAllEnvs();
  });

  it("Không có RABBITMQ_URL thì không kết nối (chỉ TTL)", async () => {
    consumeMessagesPublished.mockClear();
    vi.stubEnv("NEXT_RUNTIME", "nodejs");
    vi.stubEnv("RABBITMQ_URL", "");
    const { register } = await import("./instrumentation");
    await register();
    expect(consumeMessagesPublished).not.toHaveBeenCalled();
    vi.unstubAllEnvs();
  });
});
