-- Quyền của vai trò. Đổi quyền → thu hồi phiên của người có vai trò (trigger, functions/revoke_sessions_on_role_permission_change.sql).
CREATE TABLE role_permission (
  role_id         uuid NOT NULL REFERENCES role (id) ON DELETE CASCADE,
  permission_code text NOT NULL REFERENCES permission (code),
  PRIMARY KEY (role_id, permission_code)
);

CREATE TRIGGER role_permission_revoke_sessions
AFTER INSERT OR UPDATE OR DELETE ON role_permission
FOR EACH ROW EXECUTE FUNCTION revoke_sessions_on_role_permission_change();
