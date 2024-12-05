# Dockerfile for Microservice1
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Microservice1/Microservice1.csproj", "Microservice1/"]
RUN dotnet restore "Microservice1/Microservice1.csproj"
COPY . .
WORKDIR "/src/Microservice1"
RUN dotnet build "Microservice1.csproj" -c Release -o /app/build
RUN dotnet publish "Microservice1.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Microservice1.dll"]
