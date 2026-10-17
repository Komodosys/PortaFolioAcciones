# Imagen de la API. Construccion en dos etapas: se compila con el SDK y se ejecuta con el runtime,
# que es mucho mas pequeno y no lleva compilador dentro.

# ------------------------------------------------------------------------------------------------
# Etapa 1: compilar y publicar
# ------------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS compilar
WORKDIR /origen

# Se copian PRIMERO los ficheros de proyecto y se restaura. Asi, mientras no cambien las
# dependencias, Docker reutiliza la capa y no vuelve a descargar paquetes en cada compilacion.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/PortaFolioAcciones.Domain/PortaFolioAcciones.Domain.csproj src/PortaFolioAcciones.Domain/
COPY src/PortaFolioAcciones.Application/PortaFolioAcciones.Application.csproj src/PortaFolioAcciones.Application/
COPY src/PortaFolioAcciones.Infrastructure/PortaFolioAcciones.Infrastructure.csproj src/PortaFolioAcciones.Infrastructure/
COPY src/PortaFolioAcciones.Api/PortaFolioAcciones.Api.csproj src/PortaFolioAcciones.Api/
RUN dotnet restore src/PortaFolioAcciones.Api/PortaFolioAcciones.Api.csproj

# Y ahora el codigo.
COPY src/ src/
RUN dotnet publish src/PortaFolioAcciones.Api/PortaFolioAcciones.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app

# ------------------------------------------------------------------------------------------------
# Etapa 2: ejecutar
# ------------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS ejecutar
WORKDIR /app

# curl se instala SOLO para que Docker pueda comprobar la salud de la API: la imagen de runtime no
# trae ninguna herramienta con la que hacer una peticion HTTP.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# La imagen oficial ya trae un usuario sin privilegios llamado 'app'. No se ejecuta como root.
RUN mkdir -p /app/registros && chown -R app:app /app
USER app

COPY --from=compilar /app .

# El puerto se fija aqui y no se publica al exterior: solo Caddy habla con la API.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "PortaFolioAcciones.Api.dll"]
