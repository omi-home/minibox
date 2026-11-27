import { expect, test, vi } from "vitest";
import { type Connect, consumeMessagesPublished, MESSAGES_PUBLISHED_EXCHANGE, parseMessagesPublished } from "./events";

test("Consumer dựng queue tạm bind exchange fanout (cả khi kết nối lại) và gọi onEvent với sự kiện hợp lệ", async () => {
  let deliver: (msg: { content: Uint8Array } | null) => void = () => {};
  const ch = {
    assertExchange: vi.fn(async () => ({})),
    assertQueue: vi.fn(async () => ({ queue: "amq.gen-1" })),
    bindQueue: vi.fn(async () => ({})),
    consume: vi.fn(async (_q: string, fn: typeof deliver) => {
      deliver = fn;
      return {};
    }),
  };
  const model = { createChannel: vi.fn(async () => ch), on: vi.fn(), close: vi.fn(async () => {}) };
  let setup: ((m: typeof model) => Promise<void>) | undefined;
  const connect = vi.fn(async (_url: string, o: { recovery: { setup: typeof setup } }) => {
    setup = o.recovery.setup;
    await setup!(model);
    return model;
  }) as unknown as Connect;
  const onEvent = vi.fn();

  const close = await consumeMessagesPublished("amqp://mq", onEvent, connect);
  expect(ch.assertExchange).toHaveBeenCalledWith(MESSAGES_PUBLISHED_EXCHANGE, "fanout", { durable: true });
  expect(ch.bindQueue).toHaveBeenCalledWith("amq.gen-1", MESSAGES_PUBLISHED_EXCHANGE, "");
  expect(model.on).toHaveBeenCalledWith("error", expect.any(Function));

  deliver({ content: new TextEncoder().encode('{"locale":"en","namespace":"shop","keys":["a"]}') });
  deliver({ content: new TextEncoder().encode("rác") });
  deliver(null);
  expect(onEvent).toHaveBeenCalledTimes(1);
  expect(onEvent).toHaveBeenCalledWith({ locale: "en", namespace: "shop", keys: ["a"] });

  await setup!(model); // kết nối lại: dựng lại binding
  expect(ch.bindQueue).toHaveBeenCalledTimes(2);
  await close();
  expect(model.close).toHaveBeenCalled();
});

test("parseMessagesPublished từ chối thân sai hợp đồng", () => {
  expect(parseMessagesPublished(new TextEncoder().encode('{"locale":"en","keys":[]}'))).toBeUndefined();
  expect(parseMessagesPublished(new TextEncoder().encode("null"))).toBeUndefined();
  expect(parseMessagesPublished(new TextEncoder().encode('{"locale":"en","namespace":"admin","keys":["x",1]}'))).toEqual({ locale: "en", namespace: "admin", keys: ["x"] });
});
