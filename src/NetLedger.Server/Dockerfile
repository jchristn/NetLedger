# NetLedger Server Dockerfile
# Multi-stage build for .NET 10.0 application

# Stage 1: Build (always on amd64 to avoid QEMU issues, cross-compile for target)
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

# Copy solution and project files
COPY src/NetLedger.sln ./src/
COPY src/NetLedger/NetLedger.csproj ./src/NetLedger/
COPY src/NetLedger.Server/NetLedger.Server.csproj ./src/NetLedger.Server/

# Restore dependencies for target architecture
RUN dotnet restore ./src/NetLedger.Server/NetLedger.Server.csproj \
    /p:TargetFramework=net10.0 \
    -a $TARGETARCH

# Copy source code
COPY src/NetLedger/ ./src/NetLedger/
COPY src/NetLedger.Server/ ./src/NetLedger.Server/

# Build and publish for target architecture
RUN dotnet publish ./src/NetLedger.Server/NetLedger.Server.csproj \
    -c Release \
    -f net10.0 \
    -o /app/publish \
    -a $TARGETARCH \
    /p:TargetFramework=net10.0 \
    /p:ErrorOnDuplicatePublishOutputFiles=false \
    /p:GeneratePackageOnBuild=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install networking tools and utilities
RUN apt-get update && apt-get install -y --no-install-recommends \
    iputils-ping \
    traceroute \
    dnsutils \
    curl \
    wget \
    vim \
    net-tools \
    iproute2 \
    && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=build /app/publish .

# Create directory for data persistence
RUN mkdir -p /app/data

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Expose the default port
EXPOSE 8080

# Set the entrypoint
ENTRYPOINT ["dotnet", "NetLedger.Server.dll"]
