import createClient, { type Client, type ClientOptions } from "openapi-fetch";
import type { components as IdentityComponents, paths as IdentityPaths } from "./generated/identity";

export type { IdentityComponents, IdentityPaths };

/** Client có kiểu của service identity, sinh từ `contracts/openapi/identity.json`. */
export type IdentityClient = Client<IdentityPaths>;

const SAFE = new Set(["GET", "HEAD", "OPTIONS"]);

/**
 * Tạo client openapi-fetch cho một service; request ghi tự kèm `Idempotency-Key` mới nếu chưa có (spec: mọi API ghi bắt buộc).
 *
 * @param baseUrl - Gốc URL của service, ví dụ `http://identity:8080`.
 * @param options - Tuỳ chọn openapi-fetch (headers chung như `authorization`, `fetch` thay thế…).
 * @returns Client có kiểu theo `Paths`.
 */
export function createApiClient<Paths extends object>(baseUrl: string, options: Omit<ClientOptions, "baseUrl"> = {}): Client<Paths> {
  const client = createClient<Paths>({ ...options, baseUrl });
  client.use({
    onRequest({ request }) {
      if (!SAFE.has(request.method) && !request.headers.has("idempotency-key")) request.headers.set("idempotency-key", crypto.randomUUID());
      return request;
    },
  });
  return client;
}

/**
 * Tạo client cho service identity.
 *
 * @param baseUrl - Gốc URL nội bộ của identity.
 * @param options - Tuỳ chọn openapi-fetch.
 * @returns Client identity có kiểu.
 */
export const createIdentityClient = (baseUrl: string, options?: Omit<ClientOptions, "baseUrl">): IdentityClient =>
  createApiClient<IdentityPaths>(baseUrl, options);
