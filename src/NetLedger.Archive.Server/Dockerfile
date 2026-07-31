# NetLedger Archive Server Dockerfile

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

COPY src/NetLedger.sln ./src/
COPY src/NetLedger/NetLedger.csproj ./src/NetLedger/
COPY src/NetLedger.Archive/NetLedger.Archive.csproj ./src/NetLedger.Archive/
COPY src/NetLedger.Archive.Server/NetLedger.Archive.Server.csproj ./src/NetLedger.Archive.Server/

RUN dotnet restore ./src/NetLedger.Archive.Server/NetLedger.Archive.Server.csproj \
    /p:TargetFramework=net10.0 \
    -a $TARGETARCH

COPY src/NetLedger/ ./src/NetLedger/
COPY src/NetLedger.Archive/ ./src/NetLedger.Archive/
COPY src/NetLedger.Archive.Server/ ./src/NetLedger.Archive.Server/

RUN dotnet publish ./src/NetLedger.Archive.Server/NetLedger.Archive.Server.csproj \
    -c Release \
    -f net10.0 \
    -o /app/publish \
    -a $TARGETARCH \
    /p:TargetFramework=net10.0 \
    /p:ErrorOnDuplicatePublishOutputFiles=false \
    /p:GeneratePackageOnBuild=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

RUN mkdir -p /app/data /archive-data

ENV ASPNETCORE_URLS=http://+:8081
ENV DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8081

ENTRYPOINT ["dotnet", "NetLedger.Archive.Server.dll"]
