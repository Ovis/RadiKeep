FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
ARG APP_VERSION=0.0.0-dev

RUN apt-get update \
    && apt-get install -y --no-install-recommends nodejs npm \
    && rm -rf /var/lib/apt/lists/*

COPY ["RadiKeep/RadiKeep.csproj", "RadiKeep/"]
COPY ["RadiKeep.Logics/RadiKeep.Logics.csproj", "RadiKeep.Logics/"]
COPY ["RadiKeep/package.json", "RadiKeep/"]
COPY ["RadiKeep/package-lock.json", "RadiKeep/"]
RUN dotnet restore "RadiKeep/RadiKeep.csproj"
RUN cd RadiKeep && npm ci

COPY . .
RUN dotnet publish "RadiKeep/RadiKeep.csproj" -c Release -o /app/publish /p:UseAppHost=false /p:Version=${APP_VERSION} /p:InformationalVersion=${APP_VERSION}
RUN cp /src/README.MD /app/publish/README.MD \
    && cp /src/THIRD_PARTY_NOTICES.md /app/publish/THIRD_PARTY_NOTICES.md \
    && if [ -d /src/THIRD_PARTY_LICENSES ]; then cp -a /src/THIRD_PARTY_LICENSES /app/publish/THIRD_PARTY_LICENSES; fi \
    && if [ -d /src/THIRD_PARTY_LICENCES ]; then cp -a /src/THIRD_PARTY_LICENCES /app/publish/THIRD_PARTY_LICENSES; fi \
    && if [ -f /src/LICENSE ]; then cp /src/LICENSE /app/publish/LICENSE; fi \
    && if [ -f /src/LICENSE.MD ]; then cp /src/LICENSE.MD /app/publish/LICENSE.MD; fi

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8085
ENV ASPNETCORE_URLS=http://0.0.0.0:8085

RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "RadiKeep.dll"]
