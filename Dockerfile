# --- build ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first to maximise layer caching on dependency restore
COPY ["Pokedex.slnx", "./"]
COPY ["src/Pokedex.Api/Pokedex.Api.csproj", "src/Pokedex.Api/"]
COPY ["src/Pokedex.Core/Pokedex.Core.csproj", "src/Pokedex.Core/"]
RUN dotnet restore "src/Pokedex.Api/Pokedex.Api.csproj"

# Copy the rest of the source and publish a release build
COPY . .
RUN dotnet publish "src/Pokedex.Api/Pokedex.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# --- runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

USER app

ENTRYPOINT ["dotnet", "Pokedex.Api.dll"]
