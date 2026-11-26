-- API quản trị phân công (spec §3.6): ghi ai cấp phân công; mọi thay đổi ghi audit_log trước/sau trong cùng transaction.
-- granted_by NULL cho phân công do migration/seed tạo.
ALTER TABLE role_assignment ADD COLUMN granted_by uuid REFERENCES account (id);

-- TRUNCATE không chạy trigger FOR EACH ROW thu hồi phiên (0008): role app không được TRUNCATE bảng quyền.
REVOKE TRUNCATE ON role_assignment, role_permission FROM identity_app;

-- Trigger chạy với search_path của người gọi: ghim schema để không bị bảng giả trong schema khác che.
ALTER FUNCTION revoke_sessions_on_assignment_change() SET search_path = identity, pg_temp;
ALTER FUNCTION revoke_sessions_on_role_permission_change() SET search_path = identity, pg_temp;
