FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY backend-csharp/ backend-csharp/
COPY frontend-blazor/ frontend-blazor/

RUN dotnet restore frontend-blazor/Bioma.Client/Bioma.Client.csproj
RUN dotnet publish frontend-blazor/Bioma.Client/Bioma.Client.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Bioma.Client.dll"]
