FROM mcr.microsoft.com/dotnet/core/runtime:3.1-buster-slim-arm32v7 AS base

RUN apt-get update && \
    apt-get install -y --no-install-recommends unzip procps && \
    rm -rf /var/lib/apt/lists/*

RUN useradd -ms /bin/bash moduleuser
USER moduleuser
RUN curl -sSL https://aka.ms/getvsdbgsh | bash /dev/stdin -v latest -l ~/vsdbg

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster-arm32v7 AS build-env
WORKDIR /app

COPY *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Debug -o out

FROM base
WORKDIR /app
COPY --from=build-env /app/out ./

ENTRYPOINT ["dotnet", "iotedgeSerial.dll"]