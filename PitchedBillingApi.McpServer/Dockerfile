# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY PitchedBillingApi.McpServer/PitchedBillingApi.McpServer.csproj PitchedBillingApi.McpServer/
RUN dotnet restore "PitchedBillingApi.McpServer/PitchedBillingApi.McpServer.csproj"

# Copy source code and build
COPY PitchedBillingApi.McpServer/. PitchedBillingApi.McpServer/
WORKDIR "/src/PitchedBillingApi.McpServer"
RUN dotnet build "PitchedBillingApi.McpServer.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "PitchedBillingApi.McpServer.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PitchedBillingApi.McpServer.dll"]
