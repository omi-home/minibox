-- Locale cấu hình được (spec §6.6): admin thêm ngôn ngữ không cần deploy; vi mặc định và fallback cuối.
CREATE TABLE locale (
  code          text        PRIMARY KEY CHECK (code ~ '^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$'), -- BCP 47: vi, en, zh-Hant
  name          text        NOT NULL CHECK (length(name) BETWEEN 1 AND 100),                -- tên tự gọi: Tiếng Việt, English
  enabled       boolean     NOT NULL DEFAULT false,
  sort_order    integer     NOT NULL DEFAULT 0,
  fallback_code text        REFERENCES locale (code) CHECK (fallback_code <> code),         -- NULL = rơi về vi (fallback cuối)
  version       bigint      NOT NULL DEFAULT 0
);
