FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore Dsw2025Tpi.Api/Dsw2025Tpi.Api.csproj
RUN dotnet publish Dsw2025Tpi.Api/Dsw2025Tpi.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Dsw2025Tpi.Api.dll"]
