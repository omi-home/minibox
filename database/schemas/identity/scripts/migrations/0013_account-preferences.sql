-- Tùy chọn hiển thị của tài khoản (spec §6.6, CR #387): locale BCP 47 (NULL = chưa chọn, BFF theo cookie/Accept-Language) và
-- múi giờ IANA (mặc định giờ Việt Nam). Ứng dụng kiểm múi giờ có thật; DB chỉ chặn dạng.
ALTER TABLE account
  ADD COLUMN locale    text CHECK (locale ~ '^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$'),
  ADD COLUMN time_zone text NOT NULL DEFAULT 'Asia/Ho_Chi_Minh' CHECK (time_zone ~ '^[A-Za-z0-9_+-]+(/[A-Za-z0-9_+-]+)*$' AND length(time_zone) <= 64);
