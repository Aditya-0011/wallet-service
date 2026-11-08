FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0.403-alpine3.24 AS build
ARG BUILD_CONFIGURATION=Release
ARG TARGETARCH

WORKDIR /src

COPY ["wallet.csproj", "./"]

RUN --mount=type=secret,id=GITHUB_TOKEN \
    --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet nuget add source "https://nuget.pkg.github.com/Aditya-0011/index.json" -n github -u Aditya-0011 -p $(cat /run/secrets/GITHUB_TOKEN) --store-password-in-clear-text && \
    dotnet restore "wallet.csproj" -a $TARGETARCH

COPY . .

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish "wallet.csproj" -c $BUILD_CONFIGURATION -a $TARGETARCH \
    --self-contained \
    -p:PublishTrimmed=true \
    -p:TrimMode=partial \
    -p:InvariantGlobalization=true \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0.14-alpine3.24 AS final
RUN apk add --no-cache tzdata
WORKDIR /app

COPY --from=build /app/publish .

ENV DOTNET_EnableDiagnostics=0

USER $APP_UID

EXPOSE 7297

ENTRYPOINT ["./wallet"]
