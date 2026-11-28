-- Sản phẩm (BRD BR-CAT-01/02): thuộc một nhóm (product_type) và một danh mục, nhiều bộ sưu tập/thẻ (product_taxon);
-- tên/mô tả/slug/SEO theo locale, lịch sử slug để redirect 301 (spec §6.6); giá trị thuộc tính mô tả theo bộ thuộc tính của nhóm.
-- status mở rộng ở task duyệt sản phẩm.

CREATE TABLE product (
  id              uuid    PRIMARY KEY,                              -- UUIDv7 do ứng dụng sinh
  product_type_id uuid    NOT NULL REFERENCES product_type (id),
  category_id     uuid    NOT NULL REFERENCES category (id),
  status          text    NOT NULL DEFAULT 'draft' CHECK (status IN ('draft')),
  version         bigint  NOT NULL DEFAULT 0
);

CREATE INDEX product_category_idx ON product (category_id);
CREATE INDEX product_product_type_idx ON product (product_type_id);

CREATE TABLE product_translation (
  product_id      uuid NOT NULL REFERENCES product (id),
  locale          text NOT NULL CHECK (locale ~ '^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$'),
  name            text NOT NULL CHECK (length(name) BETWEEN 1 AND 200),
  description     text CHECK (length(description) <= 20000),
  slug            text NOT NULL CHECK (length(slug) BETWEEN 1 AND 200 AND slug = lower(slug) AND slug !~ '[[:space:]/?#%]'),
  seo_title       text CHECK (length(seo_title) <= 200),
  seo_description text CHECK (length(seo_description) <= 500),
  PRIMARY KEY (product_id, locale)
);

CREATE UNIQUE INDEX product_translation_slug ON product_translation (locale, slug);

CREATE TABLE product_slug_history (
  product_id  uuid        NOT NULL REFERENCES product (id),
  locale      text        NOT NULL,
  slug        text        NOT NULL,
  replaced_at timestamptz NOT NULL,
  PRIMARY KEY (product_id, locale, slug)
);

CREATE INDEX product_slug_history_lookup ON product_slug_history (locale, slug);

-- Một dòng mỗi giá trị (multi_select nhiều dòng cùng thuộc tính); select/multi_select lưu mã giá trị cho phép.
-- FK attribute không cascade: sửa nhóm không được bỏ thuộc tính đang có giá trị (kiểm ở ứng dụng).
CREATE TABLE product_attribute_value (
  product_id   uuid NOT NULL REFERENCES product (id),
  attribute_id uuid NOT NULL REFERENCES attribute (id),
  value        text NOT NULL CHECK (length(value) BETWEEN 1 AND 2000),
  PRIMARY KEY (product_id, attribute_id, value)
);

CREATE INDEX product_attribute_value_attribute_idx ON product_attribute_value (attribute_id);

-- product_taxon (0006) chưa có FK product_id vì chưa có bảng product; liên kết cũ không thể trỏ tới sản phẩm thật nên bỏ.
DELETE FROM product_taxon;
ALTER TABLE product_taxon ADD CONSTRAINT product_taxon_product_id_fkey FOREIGN KEY (product_id) REFERENCES product (id);
