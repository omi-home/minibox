-- Tài khoản khách và nhân viên dùng chung một bảng, phân biệt bằng kind (spec §3.1).
-- email/username do ứng dụng chuẩn hoá chữ thường trước khi ghi; đăng nhập bằng một trong hai.
-- password_hash: chuỗi PHC Argon2id; NULL khi tài khoản chỉ đăng nhập xã hội.

CREATE TABLE account (
  id            uuid        PRIMARY KEY,           -- UUIDv7 do ứng dụng sinh
  kind          text        NOT NULL CHECK (kind IN ('customer', 'staff')),
  email         text        UNIQUE,
  username      text        UNIQUE,
  password_hash text,
  created_at    timestamptz NOT NULL DEFAULT now(),
  version       bigint      NOT NULL DEFAULT 0,
  CHECK (email IS NOT NULL OR username IS NOT NULL)
);
