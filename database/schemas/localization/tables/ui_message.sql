-- Chuỗi giao diện: một dòng mỗi (namespace, key, locale); sửa vào draft_text, xuất bản chép sang published_text (BFF chỉ đọc bản này).
CREATE TABLE ui_message (
  id             uuid        PRIMARY KEY,                                    -- UUIDv7 do ứng dụng sinh
  namespace      text        NOT NULL CHECK (namespace ~ '^[a-z][a-z0-9_.-]{0,63}$'),   -- shop, admin, common, errors
  key            text        NOT NULL CHECK (key ~ '^[A-Za-z0-9_.-]{1,200}$'),
  locale_code    text        NOT NULL REFERENCES locale (code),
  draft_text     text        NOT NULL,
  published_text text,                                                       -- NULL = chưa xuất bản lần nào
  published_at   timestamptz,
  updated_at     timestamptz NOT NULL DEFAULT now(),
  version        bigint      NOT NULL DEFAULT 0,
  UNIQUE (namespace, key, locale_code),
  CHECK ((published_text IS NULL) = (published_at IS NULL))
);

CREATE INDEX ui_message_by_locale ON ui_message (locale_code, namespace);
