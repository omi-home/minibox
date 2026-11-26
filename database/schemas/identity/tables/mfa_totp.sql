-- MFA TOTP (spec §3.5). secret_enc = nonce(12) || tag(16) || ciphertext, AES-256-GCM, AAD = account_id.
-- confirmed_at NULL = đang chờ xác nhận; last_used_step chặn phát lại mã.
CREATE TABLE mfa_totp (
  account_id     uuid        PRIMARY KEY REFERENCES account (id),
  secret_enc     bytea       NOT NULL,
  confirmed_at   timestamptz,
  last_used_step bigint
);
