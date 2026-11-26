-- Bộ đếm đăng nhập sai (spec §3.1): một dòng cho mỗi định danh ('acct:' || hex(SHA-256)) và mỗi IP ('ip:' || địa chỉ).
CREATE TABLE login_throttle (
  key             text        PRIMARY KEY,
  failures        integer     NOT NULL,
  last_failure_at timestamptz NOT NULL,
  locked_until    timestamptz
);
