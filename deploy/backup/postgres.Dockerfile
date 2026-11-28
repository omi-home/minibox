# PostgreSQL staging/production kèm pgBackRest (spec §8.6, D16). Cùng digest với deploy/compose.yaml — nâng cấp thì đổi cả hai.
# ponytail: gói pgbackrest của Alpine kéo theo postgresql18 (~vài chục MB) nhưng /usr/local/bin/postgres của image gốc vẫn đứng trước PATH.
FROM postgres:18-alpine@sha256:0115267ab5a509022df9319c77be365cef335adb71556c75f60f5360ee247e71
RUN apk add --no-cache pgbackrest
COPY pgbackrest.conf /etc/pgbackrest/pgbackrest.conf
# Chỉ chạy khi khởi tạo cluster mới; cluster đã có thì tạo stanza tay (xem pgbackrest.cron).
COPY --chmod=755 stanza-create.sh /docker-entrypoint-initdb.d/00-pgbackrest-stanza.sh
CMD ["postgres", "-c", "archive_mode=on", "-c", "archive_command=pgbackrest --stanza=minibox archive-push %p", "-c", "archive_timeout=300"]
