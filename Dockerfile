
# Build React frontend
FROM node:24-bookworm-slim AS frontend

WORKDIR /src/frontend

COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci

COPY frontend/ ./
RUN npm run build


# Build ASP.NET Core application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS publish

WORKDIR /src

COPY TelegramPostsArchive/TelegramPostsArchive.csproj TelegramPostsArchive/
RUN dotnet restore TelegramPostsArchive/TelegramPostsArchive.csproj

COPY TelegramPostsArchive/ TelegramPostsArchive/

COPY --from=frontend /src/TelegramPostsArchive/wwwroot/ TelegramPostsArchive/wwwroot/

RUN dotnet publish TelegramPostsArchive/TelegramPostsArchive.csproj \
    --configuration Release \
    --no-restore \
    --output /app \
    && chmod -R a+rX /app


# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

COPY --from=publish /app/ ./

USER $APP_UID

EXPOSE 8080

ENTRYPOINT ["dotnet", "TelegramPostsArchive.dll"]
