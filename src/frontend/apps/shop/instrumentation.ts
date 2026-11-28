import { registerOTel } from "@vercel/otel";

/**
 * Bật OpenTelemetry cho app shop (spec §8.5): span của Next.js, fetch gửi kèm `traceparent` để trace nối sang service .NET.
 * Xuất OTLP theo biến chuẩn `OTEL_EXPORTER_OTLP_ENDPOINT` (Collector); không có thì không gửi đi đâu.
 * Có `RABBITMQ_URL` thì nghe `localization.MessagesPublished` để làm mới cache chuỗi giao diện ngay (không chờ TTL).
 */
export async function register(): Promise<void> {
  registerOTel({
    serviceName: "shop",
    // Chỉ gắn traceparent cho http:// (service nội bộ trong mạng Compose / localhost khi dev); provider ngoài luôn https.
    instrumentationConfig: { fetch: { propagateContextUrls: [/^http:\/\//] } },
  });
  const rabbitMq = process.env.RABBITMQ_URL;
  if (process.env.NEXT_RUNTIME !== "nodejs" || !rabbitMq) return;
  const [{ consumeMessagesPublished }, { localization }] = await Promise.all([import("@minibox/api-clients/events"), import("./lib/i18n")]);
  // Không await: broker chưa lên thì consumer tự thử lại, không chặn BFF khởi động.
  consumeMessagesPublished(rabbitMq, (e) => localization().onMessagesPublished(e)).catch((err: unknown) =>
    console.error("Không nghe được localization.MessagesPublished:", err),
  );
}
