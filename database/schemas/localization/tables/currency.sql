-- Tiền tệ hiển thị (ISO 4217); giao dịch luôn VND.
CREATE TABLE currency (
  code       char(3)  PRIMARY KEY CHECK (code ~ '^[A-Z]{3}$'),                  -- ISO 4217
  name       text     NOT NULL CHECK (length(name) BETWEEN 1 AND 100),
  symbol     text     NOT NULL CHECK (length(symbol) BETWEEN 1 AND 8),
  minor_unit smallint NOT NULL CHECK (minor_unit BETWEEN 0 AND 4),            -- số chữ số thập phân khi hiển thị
  enabled    boolean  NOT NULL DEFAULT false,
  sort_order integer  NOT NULL DEFAULT 0,
  version    bigint   NOT NULL DEFAULT 0
);
