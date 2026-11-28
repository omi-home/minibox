#!/bin/sh
# Deploy trên homeserver (spec §8.4), do job GitHub Actions gọi qua SSH: deploy/deploy.sh <staging|production> <tag>
# Logic ở scripts/deploy.mjs (cần Node như database/bootstrap/run.mjs).
set -e
exec node "$(dirname "$0")/../scripts/deploy.mjs" "$@"
