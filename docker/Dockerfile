# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY GraphLedger.sln .
COPY src/GraphLedger.Core/GraphLedger.Core.csproj src/GraphLedger.Core/
COPY src/GraphLedger.Service/GraphLedger.Service.csproj src/GraphLedger.Service/
COPY src/GraphLedger.Cli/GraphLedger.Cli.csproj src/GraphLedger.Cli/
COPY src/GraphLedger.WebUI/GraphLedger.WebUI.csproj src/GraphLedger.WebUI/

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY src/ src/

# Build and publish
RUN dotnet publish src/GraphLedger.WebUI/GraphLedger.WebUI.csproj -c Release -o /app/webui
RUN dotnet publish src/GraphLedger.Service/GraphLedger.Service.csproj -c Release -o /app/service
RUN dotnet publish src/GraphLedger.Cli/GraphLedger.Cli.csproj -c Release -o /app/cli

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Create data directory
RUN mkdir -p /app/data

# Copy published apps
COPY --from=build /app/webui ./webui
COPY --from=build /app/service ./service
COPY --from=build /app/cli ./cli

# Add CLI to PATH
ENV PATH="/app/cli:${PATH}"

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV GraphLedger__Storage__DatabasePath=/app/data/graphledger.db

# Expose port
EXPOSE 8080

# Default to running the WebUI
WORKDIR /app/webui
ENTRYPOINT ["dotnet", "GraphLedger.WebUI.dll"]
