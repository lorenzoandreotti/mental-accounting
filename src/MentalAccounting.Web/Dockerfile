FROM node:24-bookworm-slim AS ui
WORKDIR /ui
COPY src/MentalAccounting.Web/package*.json ./
RUN npm ci --no-audit --no-fund
COPY src/MentalAccounting.Web/ ./
RUN npm run css:build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY . .
COPY --from=ui /ui/wwwroot/css/site.css src/MentalAccounting.Web/wwwroot/css/site.css
RUN dotnet publish src/MentalAccounting.Web -c Release -o /out -p:SkipUiBuild=true
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
RUN mkdir -p /data/keys && chown -R app:app /data
USER app
ENV ASPNETCORE_HTTP_PORTS=8080 DataProtection__Path=/data/keys
EXPOSE 8080
ENTRYPOINT ["dotnet", "MentalAccounting.Web.dll"]
