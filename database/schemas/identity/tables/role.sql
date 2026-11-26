-- Vai trò = tập mã quyền (sửa được). kind = customer: vai trò mẫu của khách, không gán mã quyền.
CREATE TABLE role (
  id      uuid PRIMARY KEY DEFAULT uuidv7(),
  code    text NOT NULL UNIQUE CHECK (code ~ '^[a-z]+(-[a-z]+)*$'),
  name    text NOT NULL,
  kind    text NOT NULL CHECK (kind IN ('customer', 'staff')),
  version bigint NOT NULL DEFAULT 0
);
