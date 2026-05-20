# Build context: raiz del repo (Server.csproj referencia ../Core; el Client se publica
# como single-file self-contained win-x64 para servirlo via /get-client).
# Uso: docker build -f Server/Dockerfile -t canelary-server .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Service.sln ./
COPY Core/Core.csproj Core/
COPY Server/Server.csproj Server/
COPY Client/Client.csproj Client/
RUN dotnet restore Server/Server.csproj && \
    dotnet restore Client/Client.csproj -r win-x64

COPY Core/ Core/
COPY Server/ Server/
COPY Client/ Client/

RUN dotnet publish Server/Server.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

# Publica el Client cross-target desde el SDK Linux (EnableWindowsTargeting=true en
# Client.csproj habilita restaurar referencias Windows-only). Single-file self-contained
# para que installer.ps1 lo registre como Windows Service sin depender de runtime instalado.
RUN dotnet publish Client/Client.csproj -c Release --no-restore -r win-x64 --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:DebugType=none \
    -p:DebugSymbols=false \
    -o /app/client-repo

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
COPY --from=build /app/client-repo/Client.exe ./client-repo/Client.exe
COPY --from=build /app/client-repo/installer.ps1 ./client-repo/installer.ps1

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Server.dll"]
