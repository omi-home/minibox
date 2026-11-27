-- Tài khoản khách và nhân viên dùng chung một bảng, phân biệt bằng kind (spec §3.1).
-- email/username do ứng dụng chuẩn hoá chữ thường trước khi ghi; đăng nhập bằng một trong hai.
-- password_hash: chuỗi PHC Argon2id; NULL khi tài khoản chỉ đăng nhập xã hội.
-- locale: BCP 47, NULL = chưa chọn; time_zone: IANA, mặc định giờ Việt Nam (spec §6.6).
CREATE TABLE account (
  id            uuid        PRIMARY KEY,           -- UUIDv7 do ứng dụng sinh
  kind          text        NOT NULL CHECK (kind IN ('customer', 'staff')),
  email         text        UNIQUE,
  username      text        UNIQUE,
  password_hash text,
  created_at    timestamptz NOT NULL DEFAULT now(),
  version       bigint      NOT NULL DEFAULT 0,
  locale        text        CHECK (locale ~ '^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$'),
  time_zone     text        NOT NULL DEFAULT 'Asia/Ho_Chi_Minh' CHECK (time_zone ~ '^[A-Za-z0-9_+-]+(/[A-Za-z0-9_+-]+)*$' AND length(time_zone) <= 64),
  CHECK (email IS NOT NULL OR username IS NOT NULL)
);
