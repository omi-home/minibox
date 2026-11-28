#!/bin/sh
# Chạy trong docker-entrypoint-initdb.d (user postgres, server tạm đang chạy với archive_mode=on): tạo stanza để archive-push không lỗi.
set -e
pgbackrest --stanza=minibox stanza-create
