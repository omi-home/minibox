# Image app Next.js (spec §8.3), output standalone: chỉ mã đã trace, không devDependencies/packages/config (220.1). Build từ gốc repo:
#   docker build -f src/frontend/Dockerfile --build-arg APP=shop -t minibox/frontend-shop .
# Base ghim digest; nâng cấp: docker buildx imagetools inspect <image:tag> rồi thay digest.

FROM node:24-alpine@sha256:116b800c3754b440ee0aa3ed2f20f5bf3d99a554bfd9adeea09ea8017f6e85f5 AS build
ARG APP
ENV NEXT_TELEMETRY_DISABLED=1
RUN npm i -g pnpm@12.23.0
WORKDIR /frontend
COPY src/frontend .
RUN pnpm install --frozen-lockfile && pnpm -C "apps/${APP}" build \
    && cp -r "apps/${APP}/.next/static" "apps/${APP}/.next/standalone/apps/${APP}/.next/static"

FROM node:24-alpine@sha256:116b800c3754b440ee0aa3ed2f20f5bf3d99a554bfd9adeea09ea8017f6e85f5
ARG APP
ENV NODE_ENV=production NEXT_TELEMETRY_DISABLED=1 HOSTNAME=0.0.0.0 PORT=3000 APP=${APP}
WORKDIR /app
COPY --from=build --chown=node:node /frontend/apps/${APP}/.next/standalone .
COPY --chmod=755 src/frontend/docker-entrypoint.sh /usr/local/bin/docker-entrypoint
USER node
EXPOSE 3000
HEALTHCHECK --interval=10s --timeout=5s --start-period=20s --retries=5 CMD wget -q -O /dev/null http://127.0.0.1:3000/design || exit 1
ENTRYPOINT ["docker-entrypoint"]
