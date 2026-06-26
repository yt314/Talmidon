# תלמידון (Talmidon)

מערכת לניהול ומעקב עבור מורות פרטיות — ניהול תלמידים, מעקב פדגוגי, יומן שיעורים ומעקב תשלומים.
פלטפורמה רב-דיירת (Multi-Tenant): כל מורה רואה את התלמידים שלה בלבד, מעל שכבה ציבורית פתוחה.

## טכנולוגיות
- **Backend:** ASP.NET Core Web API (.NET 10, C#) + Entity Framework Core + ASP.NET Core Identity (JWT + Refresh Tokens)
- **Frontend:** Angular 21 + PrimeNG (נושא Aura) — עברית RTL
- **Database:** PostgreSQL
- **Dev infra (Docker):** PostgreSQL + Mailpit (שרת SMTP מקומי + ממשק web)

## מבנה הפרויקט
```
Talmidon/
├── backend/
│   ├── Talmidon.Domain/          # ישויות, enums (ללא תלויות)
│   ├── Talmidon.Infrastructure/  # EF DbContext, Identity, טוקנים, מיילים, בידוד דייר
│   └── Talmidon.Api/             # Web API, קונטרולרים, JWT
├── frontend/                     # Angular + PrimeNG
└── docs/                         # מסמכי אפיון (דרישות, סכמה, מסכים)
```

## הרצה מקומית
### 1. תשתית (Docker)
```bash
docker compose up -d           # PostgreSQL (5432) + Mailpit (SMTP 1025 / UI 8025)
```
### 2. Backend
```bash
# החלת מיגרציות (פעם ראשונה / לאחר שינוי סכמה):
dotnet ef database update --project backend/Talmidon.Infrastructure --startup-project backend/Talmidon.Api
# הרצה:
dotnet run --project backend/Talmidon.Api      # http://localhost:5208
```
מיילים (אימות חשבון וכו') נצפים ב-Mailpit: http://localhost:8025
### 3. Frontend
```bash
cd frontend
npm install
npm start                       # http://localhost:4200
```

## אבטחה (תקציר)
- בידוד רב-דיירת בשלוש שכבות: Global Query Filter (קריאה) · אכיפת `TenantId` ב-`SaveChanges` (כתיבה) · FKs מורכבים `(Id, TenantId)` (מסד).
- אימות: JWT (15 דק') + Refresh Tokens עם רוטציה ו-**זיהוי שימוש-חוזר** (שלילת כל המשפחה).
- אימות מייל חובה · נעילת חשבון · Rate limiting · הרשמה ללא חשיפת קיום משתמש · authz fail-safe (`RequireAuthenticatedUser` כברירת מחדל).
- **פרודקשן:** יש לספק דרך משתני סביבה: `ConnectionStrings__Default`, `Jwt__SecretKey` (≥32 בתים), `App__ApiBaseUrl`, `App__ClientUrl`, והגדרות `Email__*`.

## תיעוד
ראו את תיקיית [docs/](docs/) — אפיון דרישות, מבנה בסיס הנתונים, ותכנון המסכים.
