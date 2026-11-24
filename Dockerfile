FROM node:20-alpine AS frontend
WORKDIR /src/frontend
COPY frontend/package*.json ./
RUN npm install
COPY frontend/ ./
RUN npm run build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY backend/SupportTracker/SupportTracker.csproj backend/SupportTracker/
RUN dotnet restore backend/SupportTracker/SupportTracker.csproj
COPY backend/SupportTracker/ backend/SupportTracker/
RUN dotnet publish backend/SupportTracker/SupportTracker.csproj -c Release -o /app/publish --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=frontend /src/frontend/dist ./wwwroot
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SupportTracker.dll"]
