-- Bảng nghiệp vụ của localization (spec §6.6, CR #387): locale cấu hình được, tiền tệ hiển thị, tỷ giá, chuỗi giao diện.
-- Không cột/enum cố định theo ngôn ngữ: bản dịch là dòng khoá (…, locale). Khớp tables/*.sql.
-- version: concurrency token của EF Core (EfRepository.UpdateAsync).

CREATE TABLE locale (
  code          text        PRIMARY KEY CHECK (code ~ '^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$'), -- BCP 47: vi, en, zh-Hant
  name          text        NOT NULL CHECK (length(name) BETWEEN 1 AND 100),                -- tên tự gọi: Tiếng Việt, English
  enabled       boolean     NOT NULL DEFAULT false,
  sort_order    integer     NOT NULL DEFAULT 0,
  fallback_code text        REFERENCES locale (code) CHECK (fallback_code <> code),         -- NULL = rơi về vi (fallback cuối)
  version       bigint      NOT NULL DEFAULT 0
);

CREATE TABLE currency (
  code       char(3)  PRIMARY KEY CHECK (code ~ '^[A-Z]{3}$'),                  -- ISO 4217
  name       text     NOT NULL CHECK (length(name) BETWEEN 1 AND 100),
  symbol     text     NOT NULL CHECK (length(symbol) BETWEEN 1 AND 8),
  minor_unit smallint NOT NULL CHECK (minor_unit BETWEEN 0 AND 4),            -- số chữ số thập phân khi hiển thị
  enabled    boolean  NOT NULL DEFAULT false,
  sort_order integer  NOT NULL DEFAULT 0,
  version    bigint   NOT NULL DEFAULT 0
);

-- Tỷ giá chỉ để hiển thị ước tính (giao dịch luôn VND): số đồng cho 1 đơn vị tiền tệ. Chỉ ghi thêm; tỷ giá hiện hành là dòng
-- effective_at mới nhất không ở tương lai. Sửa sai bằng dòng mới.
CREATE TABLE exchange_rate (
  id            uuid          PRIMARY KEY,                                   -- UUIDv7 do ứng dụng sinh
  currency_code char(3)       NOT NULL REFERENCES currency (code) CHECK (currency_code <> 'VND'),
  vnd_per_unit  numeric(20,6) NOT NULL CHECK (vnd_per_unit > 0),
  effective_at  timestamptz   NOT NULL,
  actor         text          NOT NULL,                                      -- sub người nhập hoặc 'system'
  created_at    timestamptz   NOT NULL DEFAULT now()
);

CREATE INDEX exchange_rate_current ON exchange_rate (currency_code, effective_at DESC);

REVOKE UPDATE, DELETE, TRUNCATE ON exchange_rate FROM localization_app;

-- Chuỗi giao diện: một dòng mỗi (namespace, key, locale). Sửa ghi vào draft_text; xuất bản chép sang published_text
-- (BFF chỉ đọc published_text). Lịch sử thay đổi nằm ở audit_log.
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

-- Seed khởi đầu (spec §6.6): vi mặc định, en rơi về vi; VND là tiền tệ gốc.
INSERT INTO locale (code, name, enabled, sort_order, fallback_code) VALUES
  ('vi', 'Tiếng Việt', true, 0, NULL),
  ('en', 'English',    true, 1, 'vi');

INSERT INTO currency (code, name, symbol, minor_unit, enabled, sort_order) VALUES
  ('VND', 'Việt Nam đồng', '₫', 0, true, 0);
