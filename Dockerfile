FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Sunrise.Server/Sunrise.Server.csproj", "Sunrise/Sunrise.csproj"]
RUN dotnet restore "Sunrise/Sunrise.csproj"
COPY ["Sunrise.Server/", "Sunrise/"]
WORKDIR "/src/Sunrise"
RUN dotnet build "Sunrise.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Sunrise.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app

COPY ["/sunrise.pfx", "/app/certificate.pfx"]

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Sunrise.dll"]
