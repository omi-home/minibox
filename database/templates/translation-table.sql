-- Mẫu migration bảng dịch nội dung nghiệp vụ {{table}}_translation (spec §6.6, CR #387): sản phẩm, danh mục, bài viết, banner…
-- Chép vào database/schemas/<svc>/scripts/migrations/NNNN_mo-ta.sql (và tables/{{table}}_translation.sql), thay {{table}}
-- (bảng gốc có khoá id uuid), rồi thay cột ví dụ name/description bằng các cột cần dịch của bảng đó.
-- Quy ước:
--   - Khoá gồm locale: một dòng mỗi (bản ghi, locale); không cột/enum theo ngôn ngữ, thêm ngôn ngữ không đổi schema.
--   - Bảng gốc giữ dữ liệu không dịch (giá, tồn, trạng thái…); bản vi cũng là một dòng ở đây.
--   - Đọc: ILocaleReplica.ChainAsync(locale) → WHERE locale = ANY(chain) → LocaleChain.PickPerKey (ưu tiên locale đứng trước).
--   - Cột slug (nếu có): unique theo (locale, slug); đổi slug lưu lịch sử để redirect 301 (#397).

CREATE TABLE {{table}}_translation (
  {{table}}_id uuid        NOT NULL REFERENCES {{table}} (id) ON DELETE CASCADE,
  locale       text        NOT NULL CHECK (locale ~ '^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$'),
  name         text        NOT NULL,          -- ví dụ cột dịch
  description  text,                          -- ví dụ cột dịch
  updated_at   timestamptz NOT NULL DEFAULT now(),
  version      bigint      NOT NULL DEFAULT 1,
  PRIMARY KEY ({{table}}_id, locale)
);

-- Truy vấn theo locale (danh sách, sitemap theo locale) không đi qua khoá chính bắt đầu bằng {{table}}_id.
CREATE INDEX {{table}}_translation_locale_idx ON {{table}}_translation (locale);
