import { registerOTel } from "@vercel/otel";

/**
 * Bật OpenTelemetry cho app admin (spec §8.5): span của Next.js, fetch gửi kèm `traceparent` để trace nối sang service .NET.
 * Xuất OTLP theo biến chuẩn `OTEL_EXPORTER_OTLP_ENDPOINT` (Collector); không có thì không gửi đi đâu.
 */
export function register(): void {
  registerOTel({
    serviceName: "admin",
    // Chỉ gắn traceparent cho http:// (service nội bộ trong mạng Compose / localhost khi dev); provider ngoài luôn https.
    instrumentationConfig: { fetch: { propagateContextUrls: [/^http:\/\//] } },
  });
}
