FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Sunrise.csproj", "Sunrise/Sunrise.csproj"]
RUN dotnet restore "Sunrise/Sunrise.csproj"
COPY [".", "Sunrise/"]
WORKDIR "/src/Sunrise"
RUN dotnet build "Sunrise.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Sunrise.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app

# I would be happy if someone could point me to a better way to do this
COPY ["/Dependencies/rosu_pp_ffi.so", "/app/runtimes/linux-x64/native/rosu_pp_ffi.so"]
COPY ["/sunrise.pfx", "/app/certificate.pfx"]

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Sunrise.dll"]
