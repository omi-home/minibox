-- MFA TOTP (spec §3.5). secret_enc = nonce(12) || tag(16) || ciphertext, AES-256-GCM, AAD = account_id (16 byte);
-- khóa ở secret Mfa:EncryptionKey, không nằm trong DB. confirmed_at NULL = đang chờ xác nhận.
-- last_used_step: bước TOTP đã dùng gần nhất, chặn phát lại mã.
-- ponytail: một khóa, chưa có key id; khi cần xoay khóa thì thêm cột key_version và mã hóa lại.

CREATE TABLE mfa_totp (
  account_id     uuid        PRIMARY KEY REFERENCES account (id),
  secret_enc     bytea       NOT NULL,
  confirmed_at   timestamptz,
  last_used_step bigint
);

-- Mã khôi phục: chỉ lưu SHA-256 của mã 80 bit đã chuẩn hoá; used_at khác NULL = đã dùng.
CREATE TABLE mfa_recovery_code (
  account_id uuid        NOT NULL REFERENCES account (id),
  code_hash  bytea       NOT NULL,
  used_at    timestamptz,
  PRIMARY KEY (account_id, code_hash)
);
