FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/BuildingBlocks/Sentinela.Shared/Sentinela.Shared.csproj", "BuildingBlocks/Sentinela.Shared/"]
COPY ["src/Infrastructure/Sentinela.Persistence/Sentinela.Persistence.csproj", "Infrastructure/Sentinela.Persistence/"]
COPY ["src/Infrastructure/Sentinela.MessageBus/Sentinela.MessageBus.csproj", "Infrastructure/Sentinela.MessageBus/"]
COPY ["src/Infrastructure/Sentinela.Caching/Sentinela.Caching.csproj", "Infrastructure/Sentinela.Caching/"]
COPY ["src/Services/Sentinela.Automation/Sentinela.Automation.csproj", "Services/Sentinela.Automation/"]

RUN dotnet restore "Services/Sentinela.Automation/Sentinela.Automation.csproj"

COPY src/ .
WORKDIR "/src/Services/Sentinela.Automation"
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Sentinela.Automation.dll"]
