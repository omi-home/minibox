-- Mã quyền miền.tài-nguyên.hành-động (spec §3.6). Danh mục seed bằng migration 0007.
CREATE TABLE permission (
  code        text PRIMARY KEY CHECK (code ~ '^[a-z]+(-[a-z]+)*\.[a-z]+(-[a-z]+)*\.[a-z]+(-[a-z]+)*$'),
  description text NOT NULL
);
