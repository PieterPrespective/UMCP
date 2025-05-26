# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /app

# Copy project file and restore dependencies
COPY UMCPServer.csproj .
RUN dotnet restore

# Copy source code and build
COPY . .
RUN dotnet publish -c Release -o out

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:9.0-alpine
WORKDIR /app

# Copy published application
COPY --from=build /app/out .

# Set the entrypoint
ENTRYPOINT ["./UMCPServer"]
