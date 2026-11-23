import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

/**
 * מסד הבדיקות: אותו שרת ואותו משתמש כמו בפיתוח, במסד נפרד משלו. הפרטים נלקחים
 * ממחרוזת החיבור שב-appsettings.json של ה-API, ורק שם המסד מוחלף — כך שאין כאן עותק
 * נוסף של פרטי ההתחברות. ב-CI מסד הבדיקות רץ בלי סיסמה, והיא פשוט לא נבדקת.
 */
const DATABASE = 'talmidon_e2e';

const appSettings = JSON.parse(
  readFileSync(resolve(__dirname, '../../backend/Talmidon.Api/appsettings.json'), 'utf-8')
) as { ConnectionStrings: { Default: string } };

const parts = new Map(
  appSettings.ConnectionStrings.Default.split(';')
    .filter(Boolean)
    .map(pair => pair.split('=') as [string, string])
    .map(([key, value]) => [key.trim().toLowerCase(), value.trim()])
);
parts.set('database', DATABASE);

/** בתחביר של Npgsql, בשביל ה-API. */
export const connectionString = [...parts].map(([key, value]) => `${key}=${value}`).join(';');

/** בשביל psql, שקורא את פרטי החיבור ממשתני הסביבה הסטנדרטיים של PostgreSQL. */
export const psqlEnvironment = {
  PGHOST: parts.get('host') ?? 'localhost',
  PGPORT: parts.get('port') ?? '5432',
  PGDATABASE: DATABASE,
  PGUSER: parts.get('username') ?? '',
  PGPASSWORD: parts.get('password') ?? ''
};
