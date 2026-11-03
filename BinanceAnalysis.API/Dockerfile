
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copy the complete solution.
COPY . .

# Restore dependencies.
RUN dotnet restore BinanceAnalysis.API/BinanceAnalysis.API.csproj

# Publish the API.
RUN dotnet publish BinanceAnalysis.API/BinanceAnalysis.API.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "BinanceAnalysis.API.dll"]
