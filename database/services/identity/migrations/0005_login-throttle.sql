-- Bộ đếm đăng nhập sai (spec §3.1, LoginThrottle): một dòng cho mỗi định danh và mỗi IP.
-- key = 'acct:' || hex(SHA-256(định danh chuẩn hoá)) — không lưu email/username thô; hoặc 'ip:' || địa chỉ IP.
-- Định danh không tồn tại cũng được đếm như có thật để không lộ tài khoản.
-- ponytail: chưa có job dọn; khi bảng lớn: DELETE FROM login_throttle WHERE last_failure_at < now() - interval '1 day'.

CREATE TABLE login_throttle (
  key             text        PRIMARY KEY,
  failures        integer     NOT NULL,
  last_failure_at timestamptz NOT NULL,
  locked_until    timestamptz
);
