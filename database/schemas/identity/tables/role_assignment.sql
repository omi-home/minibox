-- Phân công = người + vai trò + phạm vi ('*', 'wh:<id kho>', 'farm:<id farm>'), trùng khóa của perms trong access token.
-- granted_by NULL cho phân công do migration/seed tạo. Đổi phân công → thu hồi phiên (trigger).
CREATE TABLE role_assignment (
  account_id uuid        NOT NULL REFERENCES account (id),
  role_id    uuid        NOT NULL REFERENCES role (id),
  scope      text        NOT NULL CHECK (scope ~ '^(\*|(wh|farm):[1-9][0-9]*)$'),
  created_at timestamptz NOT NULL DEFAULT now(),
  granted_by uuid        REFERENCES account (id),
  PRIMARY KEY (account_id, role_id, scope)
);

CREATE INDEX role_assignment_role ON role_assignment (role_id);

CREATE TRIGGER role_assignment_revoke_sessions
AFTER INSERT OR UPDATE OR DELETE ON role_assignment
FOR EACH ROW EXECUTE FUNCTION revoke_sessions_on_assignment_change();
