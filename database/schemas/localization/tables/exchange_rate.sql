-- Tỷ giá hiển thị ước tính: số đồng cho 1 đơn vị; chỉ ghi thêm (quyền ở security/exchange_rate.sql), hiện hành = effective_at mới nhất.
CREATE TABLE exchange_rate (
  id            uuid          PRIMARY KEY,                                   -- UUIDv7 do ứng dụng sinh
  currency_code char(3)       NOT NULL REFERENCES currency (code) CHECK (currency_code <> 'VND'),
  vnd_per_unit  numeric(20,6) NOT NULL CHECK (vnd_per_unit > 0),
  effective_at  timestamptz   NOT NULL,
  actor         text          NOT NULL,                                      -- sub người nhập hoặc 'system'
  created_at    timestamptz   NOT NULL DEFAULT now()
);

CREATE INDEX exchange_rate_current ON exchange_rate (currency_code, effective_at DESC);
