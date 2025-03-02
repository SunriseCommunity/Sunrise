FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Sunrise.sln", "./"]
COPY ["Sunrise.Server/Sunrise.Server.csproj", "Sunrise.Server/"]
COPY ["Sunrise.API/Sunrise.API.csproj", "Sunrise.API/"]
COPY ["Sunrise.Shared/Sunrise.Shared.csproj", "Sunrise.Shared/"]

COPY . .

RUN dotnet restore "Sunrise.Server/Sunrise.Server.csproj"

WORKDIR "/src/Sunrise.Server"
RUN dotnet build "Sunrise.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Sunrise.Server.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app

# Temp fix for lib from Sunrise.Shared not being copied properly
COPY ["Sunrise.Shared/Dependencies/rosu_pp_ffi.dll", "/app/"]
COPY ["Sunrise.Shared/Dependencies/rosu_pp_ffi.so", "/app/runtimes/linux-x64/native/"]

COPY ["sunrise.pfx", "/app/certificate.pfx"]

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Sunrise.Server.dll"]
