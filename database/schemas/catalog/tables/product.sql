-- Bảng product (migration 0008_product.sql).
CREATE TABLE product (
  id              uuid    PRIMARY KEY,                              -- UUIDv7 do ứng dụng sinh
  product_type_id uuid    NOT NULL REFERENCES product_type (id),
  category_id     uuid    NOT NULL REFERENCES category (id),
  status          text    NOT NULL DEFAULT 'draft' CHECK (status IN ('draft')),
  version         bigint  NOT NULL DEFAULT 0
);

CREATE INDEX product_category_idx ON product (category_id);
CREATE INDEX product_product_type_idx ON product (product_type_id);
