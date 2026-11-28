#!/bin/sh
# Điểm vào image frontend: Docker secret /run/secrets/<TÊN_BIẾN> (vd ADMIN_SESSION_KEY, RABBITMQ_URL) thành biến môi trường
# của tiến trình Next — bí mật không nằm trong compose/biến môi trường của container (spec §8.4).
set -e
for f in /run/secrets/*; do
  [ -f "$f" ] || continue
  name=${f##*/}
  case "$name" in *[!A-Z0-9_]*) continue ;; esac
  export "$name=$(cat "$f")"
done
exec node "apps/$APP/server.js"
