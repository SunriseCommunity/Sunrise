FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Sunrise.Server/", "Sunrise.Server/"]
COPY ["Sunrise.API/", "Sunrise.API/"]
COPY ["Sunrise.Shared/", "Sunrise.Shared/"]

RUN dotnet restore "Sunrise.Server/Sunrise.Server.csproj"

WORKDIR "/src/Sunrise.Server"
RUN dotnet build "Sunrise.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Sunrise.Server.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app

COPY ["sunrise.pfx", "/app/certificate.pfx"]

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Sunrise.Server.dll"]
