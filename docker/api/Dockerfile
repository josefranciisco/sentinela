FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY ["src/BuildingBlocks/Sentinela.Shared/Sentinela.Shared.csproj", "BuildingBlocks/Sentinela.Shared/"]
COPY ["src/Infrastructure/Sentinela.Persistence/Sentinela.Persistence.csproj", "Infrastructure/Sentinela.Persistence/"]
COPY ["src/Infrastructure/Sentinela.MessageBus/Sentinela.MessageBus.csproj", "Infrastructure/Sentinela.MessageBus/"]
COPY ["src/Infrastructure/Sentinela.Caching/Sentinela.Caching.csproj", "Infrastructure/Sentinela.Caching/"]
COPY ["src/Services/Sentinela.Api/Sentinela.Api.csproj", "Services/Sentinela.Api/"]

# Restore
RUN dotnet restore "Services/Sentinela.Api/Sentinela.Api.csproj"

# Copy all source
COPY src/ .

# Build
WORKDIR "/src/Services/Sentinela.Api"
RUN dotnet build -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Sentinela.Api.dll"]
