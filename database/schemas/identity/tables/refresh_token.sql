-- Refresh token (spec §3.2): chỉ lưu SHA-256 của token; mỗi lần dùng xoay sang token mới cùng family.
-- Token đã dùng (used_at) bị đưa lại → thu hồi cả family. family_expires_at NULL = không giới hạn tuyệt đối (khách).
CREATE TABLE refresh_token (
  token_hash        bytea       PRIMARY KEY,
  family_id         uuid        NOT NULL,
  account_id        uuid        NOT NULL REFERENCES account (id),
  auth_time         timestamptz NOT NULL,
  amr               text[]      NOT NULL,
  issued_at         timestamptz NOT NULL,
  expires_at        timestamptz NOT NULL,
  family_expires_at timestamptz,
  used_at           timestamptz,
  revoked_at        timestamptz,
  version           bigint      NOT NULL DEFAULT 0
);

CREATE INDEX refresh_token_family ON refresh_token (family_id);
CREATE INDEX refresh_token_account ON refresh_token (account_id) WHERE revoked_at IS NULL; -- thu hồi khi đổi quyền
