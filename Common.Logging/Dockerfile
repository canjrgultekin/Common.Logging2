# Dockerfile for Common.Logging
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Common.Logging/Common.Logging.csproj", "Common.Logging/"]
RUN dotnet restore "Common.Logging/Common.Logging.csproj"
COPY . .
WORKDIR "/src/Common.Logging"
RUN dotnet build "Common.Logging.csproj" -c Release -o /app/build
RUN dotnet publish "Common.Logging.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Common.Logging.dll"]
