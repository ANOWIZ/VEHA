# Multi-stage сборка .NET 8 API. Контекст сборки — каталог backend-dotnet.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore Veha.sln
RUN dotnet publish src/Veha.Api/Veha.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8000
ENV ASPNETCORE_URLS=http://+:8000
ENTRYPOINT ["dotnet", "Veha.Api.dll"]
