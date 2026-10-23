# --- ETAPA 1: Compilación y Publicación (Build) ---
# Usamos la imagen oficial que contiene el SDK completo de .NET para compilar
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiamos la solución y los proyectos para restaurar dependencias
COPY ["Dsw2025Tpi.sln", "./"]
COPY ["Dsw2025Tpi.Api/Dsw2025Tpi.Api.csproj", "Dsw2025Tpi.Api/"]
COPY ["Dsw2025Tpi.Application/Dsw2025Tpi.Application.csproj", "Dsw2025Tpi.Application/"]
COPY ["Dsw2025Tpi.Data/Dsw2025Tpi.Data.csproj", "Dsw2025Tpi.Data/"]
COPY ["Dsw2025Tpi.Domain/Dsw2025Tpi.Domain.csproj", "Dsw2025Tpi.Domain/"]

RUN dotnet restore "Dsw2025Tpi.sln"

# Copiamos todo el código fuente restante
COPY . .
WORKDIR "/src/Dsw2025Tpi.Api"
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# --- ETAPA 2: Ejecución Liviana (Runtime) ---
# Usamos una imagen ultra liviana que solo tiene el entorno de ejecución, sin compilador
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080

# Copiamos únicamente los archivos publicados desde la etapa anterior
COPY --from=build /app/publish .

# Comando final que enciende la API
ENTRYPOINT ["dotnet", "Dsw2025Tpi.Api.dll"]