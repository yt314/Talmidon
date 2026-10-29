import { defineConfig, devices } from '@playwright/test';
import { connectionString } from './e2e/database';

/**
 * בדיקות דפדפן מול שרת אמיתי ומסד נתונים אמיתי. מריצים עם `npm run e2e`, שבונה את
 * האתר בתצורת e2e ואז מריץ את הבדיקות. השרת והאתר עולים מכאן; Postgres צריך לרוץ
 * (בפיתוח: docker compose up db).
 */
export default defineConfig({
  testDir: './e2e',
  testMatch: '**/*.e2e.ts',
  // כל קובץ בונה את הנתונים שלו בסדר; בתוכו הצעדים תלויים זה בזה
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: 0,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: 'http://localhost:4200',
    locale: 'he-IL',
    timezoneId: 'Asia/Jerusalem',
    actionTimeout: 15_000,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure'
  },
  projects: [
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        // בסביבה שבה הדפדפן כבר מותקן מראש, בלי להוריד אותו שוב
        launchOptions: process.env.PLAYWRIGHT_CHROMIUM_PATH ? { executablePath: process.env.PLAYWRIGHT_CHROMIUM_PATH } : {}
      }
    }
  ],
  webServer: [
    {
      command: 'dotnet run --project ../backend/Talmidon.Api --no-launch-profile --urls http://localhost:5208',
      url: 'http://localhost:5208/api/health',
      timeout: 180_000,
      reuseExistingServer: !process.env.CI,
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development',
        ConnectionStrings__Default: connectionString,
        Jwt__SecretKey: 'e2e-only-signing-key-not-for-production-0123456789',
        App__ApiBaseUrl: 'http://localhost:5208',
        App__ClientUrl: 'http://localhost:4200',
        // כל הבדיקות יוצאות מאותה כתובת IP, ומגבלת ההתחברות הרגילה הייתה חוסמת אותן
        RateLimiting__Auth__PermitLimit: '1000'
      }
    },
    {
      command: 'node e2e/serve.mjs',
      url: 'http://localhost:4200',
      reuseExistingServer: !process.env.CI
    }
  ]
});
