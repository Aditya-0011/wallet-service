FROM --platform=linux/arm64 mcr.microsoft.com/dotnet/sdk:10.0-jammy AS build
ARG BUILD_CONFIGURATION=Release

WORKDIR /src

COPY ["Wallet.csproj", "./"]
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages dotnet restore "Wallet.csproj"

COPY . .

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages dotnet publish "Wallet.csproj" -c $BUILD_CONFIGURATION --no-self-contained -o /app/publish

FROM --platform=linux/arm64 mcr.microsoft.com/dotnet/aspnet:10.0-jammy AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:7297
ENV DOTNET_EnableDiagnostics=0

EXPOSE 7297

USER $APP_UID

ENTRYPOINT ["dotnet", "Wallet.dll"]
CMD []
