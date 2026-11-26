-- TRUNCATE không chạy trigger FOR EACH ROW thu hồi phiên: role app không được TRUNCATE bảng quyền.
REVOKE TRUNCATE ON role_assignment FROM identity_app;
