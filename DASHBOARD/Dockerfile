# Build the .NET API image.
# Run from repo root: docker build -f DASHBOARD/Dockerfile -t dashboard-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY DASHBOARD/DASHBOARD.csproj DASHBOARD/
RUN dotnet restore DASHBOARD/DASHBOARD.csproj
COPY DASHBOARD/ DASHBOARD/
RUN dotnet publish DASHBOARD/DASHBOARD.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "DASHBOARD.dll"]
