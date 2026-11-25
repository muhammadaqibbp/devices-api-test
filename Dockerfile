# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the layer is cached until a project file changes.
COPY DevicesApi.sln ./
COPY src/DevicesApi/DevicesApi.csproj src/DevicesApi/
COPY tests/DevicesApi.Tests/DevicesApi.Tests.csproj tests/DevicesApi.Tests/
RUN dotnet restore DevicesApi.sln

COPY src/ src/
RUN dotnet publish src/DevicesApi/DevicesApi.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./

# The official image ships a non-root "app" user.
USER $APP_UID

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "DevicesApi.dll"]
