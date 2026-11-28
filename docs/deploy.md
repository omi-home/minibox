# Deploy lên homeserver: cấu hình một lần

Luồng (spec §8.3, `.github/workflows/deploy.yml`):

- Merge vào `develop`: build 4 image (`src/backend/Dockerfile`, `src/frontend/Dockerfile`), đẩy `ghcr.io/omi-home/minibox/<service>:<commit>`, rồi deploy staging.
- Release lên `main` (PR develop→main, merge commit): chờ owner duyệt Environment `production`, rồi deploy production đúng commit develop đang chạy staging. Nếu tag hoặc digest khác staging thì `deploy.sh` dừng (exit 3).

Job deploy vào tailnet, SSH vào homeserver, checkout commit trong `/srv/minibox/repo` rồi chạy `deploy/deploy.sh <env> <commit>`.

## GitHub (Settings của repo)

Mọi secret và biến deploy đặt trong **Environment**, không đặt ở cấp repo. Secret cấp repo thì workflow trên bất kỳ nhánh nào cũng
đọc được, nên bỏ qua được bước duyệt `production`. Mỗi Environment dùng khoá riêng (OAuth client và khoá SSH khác nhau).

| Environment | Deployment branches | Người duyệt |
|---|---|---|
| `staging` | chỉ `develop` | không |
| `production` | chỉ `main` | owner org (required reviewers) |

Trong **mỗi** Environment:

| Loại | Tên | Giá trị |
|---|---|---|
| Secret | `TS_OAUTH_CLIENT_ID`, `TS_OAUTH_SECRET` | OAuth client của Tailscale, scope `auth_keys`, gắn tag `tag:ci` |
| Secret | `DEPLOY_SSH_KEY` | Khoá riêng SSH (ed25519) chỉ dùng cho deploy môi trường đó |
| Variable | `DEPLOY_SSH_TARGET` | `root@<tên máy trong tailnet>` |
| Variable | `DEPLOY_KNOWN_HOSTS` | Kết quả `ssh-keyscan <tên máy trong tailnet>` (ghim host key) |

Ở cấp repo không được có secret nào trong số này (Settings → Secrets and variables → Actions → Repository secrets phải trống).

Package GHCR (`backend-*`, `frontend-*`) để Public sau lần build đầu (Package settings → Change visibility). Cách khác là `docker login ghcr.io` trên homeserver bằng PAT `read:packages`.

## Tailscale

ACL cho `tag:ci` chỉ vào được cổng 22 của homeserver. Node CI là node tạm thời, tự xoá khi job xong.

## Homeserver

- `git clone https://github.com/omi-home/minibox /srv/minibox/repo`. Cài Node ≥ 24 (`deploy.mjs`) và Docker.
- Khoá công khai của `DEPLOY_SSH_KEY` thêm vào `/root/.ssh/authorized_keys`. Deploy phải chạy bằng root để đổi chủ file secret sang uid user trong image.
- Secret ban đầu theo `deploy/<env>/.env.example`. Các file `*.connection`, `*rabbitmq*`, `identity_mfa.key`, `admin_session.key` do `deploy.mjs` tự sinh.
- Deploy bị ngắt giữa chừng sẽ để lại `/srv/minibox/<env>/deploy.lock`. Xoá file này trước khi chạy lại.
