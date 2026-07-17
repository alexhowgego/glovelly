FROM node:24-bookworm-slim AS frontend-build
WORKDIR /src/frontend/glovelly-web

COPY frontend/glovelly-web/package.json frontend/glovelly-web/package-lock.json ./
RUN npm ci

COPY frontend/glovelly-web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /src

COPY glovelly.sln ./
COPY Directory.Build.props Directory.Packages.props ./
COPY .config/dotnet-tools.json .config/dotnet-tools.json
COPY backend/Glovelly.Api/Glovelly.Api.csproj backend/Glovelly.Api/
COPY backend/Glovelly.Migrations/Glovelly.Migrations.csproj backend/Glovelly.Migrations/
COPY backend/Glovelly.Api.Tests/Glovelly.Api.Tests.csproj backend/Glovelly.Api.Tests/
COPY backend/Glovelly.Worker/Glovelly.Worker.csproj backend/Glovelly.Worker/
COPY backend/Glovelly.Matching/Glovelly.Matching.csproj backend/Glovelly.Matching/
COPY backend/Glovelly.Matching.Tests/Glovelly.Matching.Tests.csproj backend/Glovelly.Matching.Tests/
RUN dotnet restore glovelly.sln
RUN dotnet tool restore

COPY backend/ ./backend/
RUN dotnet publish backend/Glovelly.Api/Glovelly.Api.csproj --configuration Release --output /app/api
RUN dotnet publish backend/Glovelly.Worker/Glovelly.Worker.csproj --configuration Release --output /app/worker
RUN dotnet tool run dotnet-ef migrations bundle \
    --project backend/Glovelly.Migrations/Glovelly.Migrations.csproj \
    --startup-project backend/Glovelly.Migrations/Glovelly.Migrations.csproj \
    --context AppDbContext \
    --configuration Release \
    --self-contained \
    --runtime linux-x64 \
    --output /app/efbundle

COPY --from=frontend-build /src/frontend/glovelly-web/dist/ /app/api/wwwroot/

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ARG BUILD_COMMIT_ID=unknown
ARG BUILD_TIMESTAMP=unknown

ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
ENV App__BuildCommitId=${BUILD_COMMIT_ID}
ENV App__BuildTimestamp=${BUILD_TIMESTAMP}
EXPOSE 8080

COPY --from=backend-build /app/api ./
COPY --from=backend-build /app/worker ./worker/
COPY --from=backend-build /app/efbundle ./efbundle

ENTRYPOINT ["sh", "-c", "exec dotnet Glovelly.Api.dll --urls http://+:${PORT:-8080}"]
