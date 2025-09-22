# Use the ASP.NET base image for ARM64
FROM --platform=linux/arm64 mcr.microsoft.com/dotnet/aspnet:8.0-noble AS base
WORKDIR /app

# Use the .NET SDK image for ARM64
FROM --platform=linux/arm64 mcr.microsoft.com/dotnet/sdk:8.0-noble AS build
WORKDIR /app

# Copy the project file and restore dependencies
COPY ["MisguidedLogs.Gold.WarcraftLogs/MisguidedLogs.Gold.WarcraftLogs.csproj", "MisguidedLogs.Gold.WarcraftLogs/"]
RUN dotnet restore "./MisguidedLogs.Gold.WarcraftLogs/MisguidedLogs.Gold.WarcraftLogs.csproj"

# Copy the rest of the application code
COPY . .  
RUN dotnet build -c Release -o out

# Publish the application
FROM build AS publish
RUN dotnet publish -c Release -o out

# Final stage to create the runtime image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/out .
ENTRYPOINT ["dotnet", "MisguidedLogs.Gold.WarcraftLogs.dll"]
