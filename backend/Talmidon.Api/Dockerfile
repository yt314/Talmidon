# בונים עם ה-SDK המלא, מריצים על ה-runtime הקל בלבד. הקשר הבנייה (build context) הוא תיקיית
# backend/ כולה (ראו docker-compose.prod.yml) כדי שנוכל להעתיק גם את Talmidon.Domain/Infrastructure
# שה-API תלוי בהם (project references), לא רק את Talmidon.Api עצמו.

# Project target framework: net10.0
# Use the official Microsoft .NET 10 stable images, matching the app's target framework.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Talmidon.Domain/Talmidon.Domain.csproj Talmidon.Domain/
COPY Talmidon.Infrastructure/Talmidon.Infrastructure.csproj Talmidon.Infrastructure/
COPY Talmidon.Api/Talmidon.Api.csproj Talmidon.Api/
RUN dotnet restore Talmidon.Api/Talmidon.Api.csproj

COPY Talmidon.Domain/ Talmidon.Domain/
COPY Talmidon.Infrastructure/ Talmidon.Infrastructure/
COPY Talmidon.Api/ Talmidon.Api/
RUN dotnet publish Talmidon.Api/Talmidon.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Talmidon.Api.dll"]
