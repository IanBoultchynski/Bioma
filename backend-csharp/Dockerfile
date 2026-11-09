FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY backend-csharp/Bioma.Domain/Bioma.Domain.csproj backend-csharp/Bioma.Domain/
COPY backend-csharp/Bioma.Application/Bioma.Application.csproj backend-csharp/Bioma.Application/
COPY backend-csharp/Bioma.Infrastructure/Bioma.Infrastructure.csproj backend-csharp/Bioma.Infrastructure/
COPY backend-csharp/Bioma.Api/Bioma.Api.csproj backend-csharp/Bioma.Api/

RUN dotnet restore backend-csharp/Bioma.Api/Bioma.Api.csproj

COPY backend-csharp/ backend-csharp/
RUN dotnet publish backend-csharp/Bioma.Api/Bioma.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Bioma.Api.dll"]
