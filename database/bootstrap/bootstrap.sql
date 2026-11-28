-- Bootstrap database `minibox` (spec §4.1–4.3). Idempotent; chạy bằng db_admin qua `database/bootstrap/run.mjs`.
-- run.mjs đặt trước 3 setting: minibox.services (csv), minibox.passwords (jsonb role→mật khẩu),
-- minibox.new_passwords (csv role vừa sinh mật khẩu — role đã tồn tại thì đặt lại cho khớp file secret).
\set ON_ERROR_STOP on
SET client_min_messages = warning;

REVOKE ALL ON DATABASE minibox FROM PUBLIC;
REVOKE ALL ON SCHEMA public FROM PUBLIC;

CREATE SCHEMA IF NOT EXISTS extensions;
REVOKE ALL ON SCHEMA extensions FROM PUBLIC;
CREATE EXTENSION IF NOT EXISTS unaccent SCHEMA extensions;
CREATE EXTENSION IF NOT EXISTS pg_trgm SCHEMA extensions;

DO $$
DECLARE
  svc text;
  r text;
  pw jsonb := current_setting('minibox.passwords')::jsonb;
  fresh text[] := string_to_array(current_setting('minibox.new_passwords'), ',');
  search_services text[] := ARRAY['catalog']; -- service dùng unaccent/pg_trgm (spec D8)
BEGIN
  FOREACH svc IN ARRAY string_to_array(current_setting('minibox.services'), ',') LOOP
    FOREACH r IN ARRAY ARRAY[svc || '_migrator', svc || '_app'] LOOP
      IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
        EXECUTE format('CREATE ROLE %I LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE PASSWORD %L', r, pw ->> r);
      ELSIF r = ANY (fresh) THEN
        EXECUTE format('ALTER ROLE %I PASSWORD %L', r, pw ->> r);
      END IF;
      EXECUTE format('GRANT CONNECT ON DATABASE minibox TO %I', r);
    END LOOP;

    -- migrator: chủ schema (DDL); app: chỉ DML trên bảng do migrator tạo, không DDL, không thấy schema khác.
    EXECUTE format('CREATE SCHEMA IF NOT EXISTS %I AUTHORIZATION %I', svc, svc || '_migrator');
    EXECUTE format('REVOKE ALL ON SCHEMA %I FROM PUBLIC', svc);
    EXECUTE format('GRANT USAGE ON SCHEMA %I TO %I', svc, svc || '_app');
    EXECUTE format('ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA %I GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO %I',
                   svc || '_migrator', svc, svc || '_app');
    EXECUTE format('ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA %I GRANT USAGE, SELECT ON SEQUENCES TO %I',
                   svc || '_migrator', svc, svc || '_app');
    -- Không GRANT ... ON ALL TABLES: sẽ trả lại UPDATE/DELETE mà migration đã thu hồi trên bảng chỉ ghi thêm (spec §4.5).

    IF svc = ANY (search_services) THEN
      EXECUTE format('GRANT USAGE ON SCHEMA extensions TO %I, %I', svc || '_migrator', svc || '_app');
      EXECUTE format('ALTER ROLE %I SET search_path = %I, extensions', svc || '_migrator', svc);
      EXECUTE format('ALTER ROLE %I SET search_path = %I, extensions', svc || '_app', svc);
    ELSE
      EXECUTE format('ALTER ROLE %I SET search_path = %I', svc || '_migrator', svc);
      EXECUTE format('ALTER ROLE %I SET search_path = %I', svc || '_app', svc);
    END IF;
  END LOOP;
END
$$;
