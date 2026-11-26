-- Mã khôi phục MFA: chỉ lưu SHA-256 của mã đã chuẩn hoá; used_at khác NULL = đã dùng.
CREATE TABLE mfa_recovery_code (
  account_id uuid        NOT NULL REFERENCES account (id),
  code_hash  bytea       NOT NULL,
  used_at    timestamptz,
  PRIMARY KEY (account_id, code_hash)
);
