#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG AZURE_DEVOPS_PAT  # Add ARG for the PAT
WORKDIR /src

# Ensure a clean slate for NuGet sources
RUN dotnet nuget remove source Gateway_Nuget_Feed || true
RUN dotnet nuget remove source nuget.org || true

# Add the default NuGet.org source
RUN dotnet nuget add source "https://api.nuget.org/v3/index.json" --name nuget.org

# Add the private Azure DevOps NuGet feed with authentication
RUN dotnet nuget add source "https://pkgs.dev.azure.com/BritishLibrary-AppDev/Gateway/_packaging/Gateway_Nuget_Feed/nuget/v3/index.json" \
    --name Gateway_Nuget_Feed \
    --username AzureDevOps \
    --password $AZURE_DEVOPS_PAT \
    --store-password-in-clear-text

COPY ["gateway-global-integration-api/gateway-global-integration-api.csproj", "gateway-global-integration-api/"]

RUN dotnet restore "./gateway-global-integration-api/./gateway-global-integration-api.csproj"

COPY . .
WORKDIR "/src/gateway-global-integration-api"
RUN dotnet build "./gateway-global-integration-api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./gateway-global-integration-api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "gateway-global-integration-api.dll"]