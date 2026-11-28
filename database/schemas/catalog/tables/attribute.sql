-- Bảng attribute (migration 0007_product-type.sql).
CREATE TABLE attribute (
  id              uuid    PRIMARY KEY,                              -- UUIDv7 do ứng dụng sinh
  product_type_id uuid    NOT NULL REFERENCES product_type (id),
  code            text    NOT NULL CHECK (length(code) <= 50 AND code ~ '^[a-z][a-z0-9]*(-[a-z0-9]+)*$'),
  data_type       text    NOT NULL CHECK (data_type IN ('text', 'number', 'boolean', 'select', 'multi_select')),
  unit            text    CHECK (length(unit) BETWEEN 1 AND 20),
  required        boolean NOT NULL,
  variant         boolean NOT NULL,
  sort_order      integer NOT NULL,
  UNIQUE (product_type_id, code),
  CHECK (unit IS NULL OR data_type = 'number'),
  CHECK (NOT variant OR data_type = 'select')
);
