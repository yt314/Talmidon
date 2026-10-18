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

# Npgsql טוען את ספריית ה-GSSAPI של Kerberos בזמן התחברות, ו-Microsoft הסירה אותה
# מתמונות ה-runtime של .NET כדי לצמצם שטח תקיפה. התוצאה היא נפילה בעלייה:
#
#   Cannot load library libgssapi_krb5.so.2
#   Error: libgssapi_krb5.so.2: cannot open shared object file
#
# התגית 10.0 נעה, ולכן זה התפוצץ בפריסה אקראית — בלי ששום דבר בקוד השתנה; פשוט
# נבנתה תמונת בסיס חדשה שבה החבילה כבר לא קיימת. התקנה מפורשת כאן היא מה שהופך
# את זה לבלתי תלוי במה שהתמונה במעלה הזרם מחליטה לכלול החודש.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

# התמונה הזו היא תמונת הייצור. בלי הגדרה מפורשת, סביבה שנקבעת במקום אחר קובעת
# התנהגות רגישה — לוח הבקרה של Hangfire ומסמכי ה-API נחשפים ללא הזדהות במצב פיתוח.
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Talmidon.Api.dll"]
