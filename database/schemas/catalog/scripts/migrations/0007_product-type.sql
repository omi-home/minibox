-- Nhóm sản phẩm và bộ thuộc tính (BRD BR-CAT-01/02): mỗi nhóm (đèn, chậu, thủy sinh…) có thuộc tính với kiểu, đơn vị, giá trị cho phép,
-- cờ bắt buộc (chặn gửi duyệt sản phẩm) và cờ tạo biến thể (chỉ kiểu select). Nhãn theo locale (database/templates/translation-table.sql).
-- Nhóm xóa mềm bằng archived_at; thuộc tính/giá trị bị bỏ khi sửa nhóm thì xóa dòng (bảng con cascade).

CREATE TABLE product_type (
  id          uuid        PRIMARY KEY,                              -- UUIDv7 do ứng dụng sinh
  code        text        NOT NULL UNIQUE CHECK (length(code) <= 50 AND code ~ '^[a-z][a-z0-9]*(-[a-z0-9]+)*$'),
  sort_order  integer     NOT NULL DEFAULT 0,
  archived_at timestamptz,                                          -- NULL = đang hoạt động
  version     bigint      NOT NULL DEFAULT 0
);

CREATE TABLE product_type_translation (
  product_type_id uuid NOT NULL REFERENCES product_type (id),
  locale          text NOT NULL CHECK (locale ~ '^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$'),
  name            text NOT NULL CHECK (length(name) BETWEEN 1 AND 200),
  PRIMARY KEY (product_type_id, locale)
);

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

CREATE TABLE attribute_translation (
  attribute_id uuid NOT NULL REFERENCES attribute (id) ON DELETE CASCADE,
  locale       text NOT NULL CHECK (locale ~ '^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$'),
  label        text NOT NULL CHECK (length(label) BETWEEN 1 AND 200),
  PRIMARY KEY (attribute_id, locale)
);

CREATE TABLE attribute_value (
  id           uuid    PRIMARY KEY,                                 -- UUIDv7 do ứng dụng sinh
  attribute_id uuid    NOT NULL REFERENCES attribute (id) ON DELETE CASCADE,
  code         text    NOT NULL CHECK (length(code) <= 50 AND code ~ '^[a-z][a-z0-9]*(-[a-z0-9]+)*$'),
  sort_order   integer NOT NULL,
  UNIQUE (attribute_id, code)
);

CREATE TABLE attribute_value_translation (
  attribute_value_id uuid NOT NULL REFERENCES attribute_value (id) ON DELETE CASCADE,
  locale             text NOT NULL CHECK (locale ~ '^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$'),
  label              text NOT NULL CHECK (length(label) BETWEEN 1 AND 200),
  PRIMARY KEY (attribute_value_id, locale)
);
