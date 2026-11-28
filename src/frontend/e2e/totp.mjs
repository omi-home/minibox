// TOTP RFC 6238 (SHA-1, 6 số, bước 30 giây) cho e2e: identity giả kiểm mã, test tính mã như ứng dụng xác thực.
import { createHmac } from "node:crypto";

const B32 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

/** @param {Buffer} buf */
export const base32 = (buf) =>
  [...buf].map((b) => b.toString(2).padStart(8, "0")).join("").match(/.{1,5}/g).map((s) => B32[parseInt(s.padEnd(5, "0"), 2)]).join("");

/** @param {string} s */
const unbase32 = (s) => Buffer.from([...s].map((c) => B32.indexOf(c).toString(2).padStart(5, "0")).join("").match(/.{8}/g).map((b) => parseInt(b, 2)));

/**
 * @param {string} secret secret Base32
 * @param {number} step số bước 30 giây kể từ epoch
 * @returns {string} mã 6 chữ số
 */
export const totp = (secret, step) => {
  const msg = Buffer.alloc(8);
  msg.writeBigUInt64BE(BigInt(step));
  const h = createHmac("sha1", unbase32(secret)).update(msg).digest();
  const o = h[19] & 15;
  return String((h.readUInt32BE(o) & 0x7fffffff) % 1_000_000).padStart(6, "0");
};
