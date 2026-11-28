import { connect } from "amqplib";
import type { MessagesPublished } from "./localization";

/** Exchange fanout mà localization phát `localization.MessagesPublished.v1` (khai cùng tên ở Program.cs của localization). */
export const MESSAGES_PUBLISHED_EXCHANGE = "localization.MessagesPublished.v1";

/** Phần của amqplib mà consumer dùng; thay được trong test. */
export type Connect = (url: string, options: { recovery: { maxRetries: number; setup: (model: Model) => Promise<void> } }) => Promise<Model>;

type Model = {
  createChannel(): Promise<{
    assertExchange(name: string, type: "fanout", options: { durable: boolean }): Promise<unknown>;
    assertQueue(name: "", options: { exclusive: boolean; autoDelete: boolean }): Promise<{ queue: string }>;
    bindQueue(queue: string, exchange: string, pattern: ""): Promise<unknown>;
    consume(queue: string, onMessage: (msg: { content: Uint8Array } | null) => void, options: { noAck: boolean }): Promise<unknown>;
  }>;
  on(event: "error", listener: (err: Error) => void): unknown;
  close(): Promise<void>;
};

/**
 * Đọc thân message RabbitMQ thành `MessagesPublished`.
 *
 * @param content - Thân message (JSON của `MessagesPublishedV1` do Wolverine phát).
 * @returns Sự kiện; `undefined` nếu không đúng hợp đồng.
 */
export function parseMessagesPublished(content: Uint8Array): MessagesPublished | undefined {
  try {
    const e = JSON.parse(new TextDecoder().decode(content)) as Partial<MessagesPublished>;
    return typeof e.locale === "string" && typeof e.namespace === "string" && Array.isArray(e.keys)
      ? { locale: e.locale, namespace: e.namespace, keys: e.keys.filter((k): k is string => typeof k === "string") }
      : undefined;
  } catch {
    return undefined;
  }
}

/**
 * Nghe `localization.MessagesPublished.v1` qua queue tạm riêng của tiến trình (mỗi instance BFF nhận đủ mọi sự kiện).
 *
 * @param url - URI RabbitMQ (`amqp://…`, credential từ secret).
 * @param onEvent - Gọi với mỗi sự kiện hợp lệ, thường là `localization().onMessagesPublished`.
 * @param connectFn - Hàm kết nối; mặc định amqplib có tự kết nối lại.
 * @returns Hàm đóng kết nối.
 * @remarks Kết nối lại vô hạn và dựng lại queue/binding mỗi lần. Sự kiện phát lúc mất kết nối bị lỡ; TTL của cache là lưới an toàn.
 */
export async function consumeMessagesPublished(
  url: string,
  onEvent: (event: MessagesPublished) => void,
  connectFn: Connect = connect as unknown as Connect,
): Promise<() => Promise<void>> {
  const model = await connectFn(url, {
    recovery: {
      maxRetries: Infinity,
      async setup(m) {
        const ch = await m.createChannel();
        await ch.assertExchange(MESSAGES_PUBLISHED_EXCHANGE, "fanout", { durable: true });
        const { queue } = await ch.assertQueue("", { exclusive: true, autoDelete: true });
        await ch.bindQueue(queue, MESSAGES_PUBLISHED_EXCHANGE, "");
        await ch.consume(
          queue,
          (msg) => {
            const event = msg && parseMessagesPublished(msg.content);
            if (event) onEvent(event);
          },
          { noAck: true },
        );
      },
    },
  });
  // Bắt buộc có listener error, không thì tiến trình thoát dù đang tự kết nối lại.
  model.on("error", (err) => console.warn("localization.MessagesPublished consumer:", err.message));
  return () => model.close();
}
