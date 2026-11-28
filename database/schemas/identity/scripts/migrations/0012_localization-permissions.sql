-- Mã quyền của service localization (#391): quản trị bản dịch và ngôn ngữ. Chủ shop có mọi quyền; nhân viên nội dung soạn và xuất bản bản dịch.
INSERT INTO permission (code, description) VALUES
  ('localization.message.manage', 'Sửa bản nháp chuỗi giao diện'),
  ('localization.message.publish', 'Xuất bản chuỗi giao diện'),
  ('localization.locale.manage', 'Thêm, sửa, bật/tắt ngôn ngữ');

INSERT INTO role_permission (role_id, permission_code)
SELECT r.id, x.permission_code
FROM (VALUES
  ('owner', 'localization.message.manage'), ('owner', 'localization.message.publish'), ('owner', 'localization.locale.manage'),
  ('content', 'localization.message.manage'), ('content', 'localization.message.publish')
) AS x (role_code, permission_code)
JOIN role r ON r.code = x.role_code;
