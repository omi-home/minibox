# Image chung cho mọi service .NET (spec §8.3). Build từ gốc repo:
#   docker build -f src/backend/Dockerfile --build-arg SERVICE=identity --build-arg PROJECT=Identity -t minibox/backend-identity .
# SERVICE = tên schema/thư mục (services/<SERVICE>), PROJECT = tiền tố project (Minibox.<PROJECT>.Api).
# Base ghim digest; nâng cấp: docker buildx imagetools inspect <image:tag> rồi thay digest.

FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:8c6edfb26c85153f236d26efebd9ad349aec403583f343f201ee67838b23658d AS build
ARG SERVICE
ARG PROJECT
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
WORKDIR /repo
# Directory.Build.props nhúng database/schemas/<svc>/scripts vào Api cho chế độ --migrate.
COPY database/schemas database/schemas
COPY src/backend src/backend
WORKDIR /repo/src/backend/services/${SERVICE}/src/Minibox.${PROJECT}.Api
# OpenAPI chỉ sinh ở CI (contracts/tests), không trong image.
RUN dotnet build -c Release -p:OpenApiGenerateDocuments=false
# Wolverine production nạp code sinh sẵn (TypeLoadMode.Static): sinh vào Internal/Generated rồi publish kèm.
# Cấu hình giữ chỗ chỉ để dựng host; codegen không kết nối PostgreSQL/RabbitMQ.
RUN ConnectionStrings__App="Host=codegen" ConnectionStrings__RabbitMq="amqp://codegen" Identity__Authority="http://codegen" \
    dotnet run -c Release --no-build --no-launch-profile -- codegen write
# Không đóng gói XML doc (mô tả API nội bộ) vào image (220.1).
RUN dotnet publish -c Release -o /out -p:OpenApiGenerateDocuments=false -p:GenerateDocumentationFile=false \
    && find /out -name '*.xml' -delete && find /out -name '*.pdb' -delete \
    && ln -s "Minibox.${PROJECT}.Api.dll" /out/app.dll

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine@sha256:21c3f9798416cba4c0576a947c286231cb71ea2884896cb968253f5c9668f930
# ICU + tzdata: định dạng theo culture và múi giờ cửa hàng (Asia/Ho_Chi_Minh, spec §2.5, §6.6).
RUN apk add --no-cache icu-libs tzdata
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false ASPNETCORE_HTTP_PORTS=8080
WORKDIR /app
COPY --from=build /out .
# User không phải root có sẵn trong image aspnet (APP_UID).
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=5s --start-period=20s --retries=5 CMD wget -q -O /dev/null http://127.0.0.1:8080/health/ready || exit 1
ENTRYPOINT ["dotnet", "app.dll"]
