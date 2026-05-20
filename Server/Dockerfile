# Build context: raiz del repo (Server.csproj referencia ../Core).
# Uso: docker build -f Server/Dockerfile -t canelary-server .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Service.sln ./
COPY Core/Core.csproj Core/
COPY Server/Server.csproj Server/
RUN dotnet restore Server/Server.csproj

COPY Core/ Core/
COPY Server/ Server/
RUN dotnet publish Server/Server.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Server.dll"]
