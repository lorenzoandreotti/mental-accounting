FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY . .
RUN dotnet publish src/MentalAccounting.Web -c Release -o /out
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
RUN mkdir -p /data/keys && chown -R app:app /data
USER app
ENV ASPNETCORE_HTTP_PORTS=8080 DataProtection__Path=/data/keys
EXPOSE 8080
ENTRYPOINT ["dotnet", "MentalAccounting.Web.dll"]
