-- Mã quyền của service catalog (#482); catalog.product.manage đã có từ 0007. Chủ shop có mọi quyền; vai trò Nội dung soạn danh mục,
-- bộ sưu tập/thẻ, thuộc tính, sản phẩm, file và gửi duyệt. Duyệt sản phẩm và đặt giá chỉ chủ shop (tách người gửi/người duyệt).
INSERT INTO permission (code, description) VALUES
  ('catalog.product.submit',    'Gửi sản phẩm đi duyệt'),
  ('catalog.product.approve',   'Duyệt, từ chối, tạm dừng sản phẩm'),
  ('catalog.category.manage',   'Quản lý danh mục'),
  ('catalog.collection.manage', 'Quản lý bộ sưu tập và thẻ'),
  ('catalog.attribute.manage',  'Quản lý nhóm sản phẩm và bộ thuộc tính'),
  ('catalog.price.write',       'Đặt giá bán'),
  ('catalog.file.manage',       'Quản lý file số, nguồn quyền và giấy phép');

INSERT INTO role_permission (role_id, permission_code)
SELECT r.id, x.permission_code
FROM (VALUES
  ('owner', 'catalog.product.submit'), ('owner', 'catalog.product.approve'), ('owner', 'catalog.category.manage'),
  ('owner', 'catalog.collection.manage'), ('owner', 'catalog.attribute.manage'), ('owner', 'catalog.price.write'),
  ('owner', 'catalog.file.manage'),
  ('content', 'catalog.product.submit'), ('content', 'catalog.category.manage'), ('content', 'catalog.collection.manage'),
  ('content', 'catalog.attribute.manage'), ('content', 'catalog.file.manage')
) AS x (role_code, permission_code)
JOIN role r ON r.code = x.role_code;
