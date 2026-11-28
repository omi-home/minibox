-- Chạy mọi lần deploy, sau migrations: ghi lại cột note (0002) đã có hay chưa.
INSERT INTO deploy_log (phase)
SELECT 'post:' || EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'demo' AND table_name = 'item' AND column_name = 'note');
