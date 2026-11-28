-- Chạy mọi lần deploy, trước migrations: idempotent.
CREATE TABLE IF NOT EXISTS deploy_log (seq bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, phase text NOT NULL);
INSERT INTO deploy_log (phase) VALUES ('pre');
