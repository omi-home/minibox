-- Bảng product_taxon (migration 0006_taxon.sql).
-- product_id chưa có FK: bảng product thêm ở task Product, khi đó bổ sung REFERENCES product (id).
CREATE TABLE product_taxon (
  product_id uuid NOT NULL,
  taxon_id   uuid NOT NULL REFERENCES taxon (id),
  PRIMARY KEY (product_id, taxon_id)
);

CREATE INDEX product_taxon_taxon_idx ON product_taxon (taxon_id);
