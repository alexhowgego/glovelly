FROM node:24-bookworm-slim AS frontend-build
WORKDIR /src/frontend/glovelly-web

COPY frontend/glovelly-web/package.json frontend/glovelly-web/package-lock.json ./
RUN npm ci

COPY frontend/glovelly-web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /src

COPY glovelly.sln ./
COPY backend/Glovelly.Api/Glovelly.Api.csproj backend/Glovelly.Api/
COPY backend/Glovelly.Api.Tests/Glovelly.Api.Tests.csproj backend/Glovelly.Api.Tests/
RUN dotnet restore glovelly.sln

COPY backend/ ./backend/
RUN dotnet publish backend/Glovelly.Api/Glovelly.Api.csproj --configuration Release --output /app/publish

COPY --from=frontend-build /src/frontend/glovelly-web/dist/ /app/publish/wwwroot/

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ARG BUILD_COMMIT_ID=unknown
ARG BUILD_TIMESTAMP=unknown

ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
ENV App__BuildCommitId=${BUILD_COMMIT_ID}
ENV App__BuildTimestamp=${BUILD_TIMESTAMP}
EXPOSE 8080

COPY --from=backend-build /app/publish ./

ENTRYPOINT ["sh", "-c", "exec dotnet Glovelly.Api.dll --urls http://+:${PORT:-8080}"]
