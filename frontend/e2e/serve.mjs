// מגיש את בניית ה-e2e עם הכותרות שב-vercel.json, כמו בפריסה האמיתית — כך שבדיקות הדפדפן
// רצות תחת אותו CSP, וחסימה שלו נכשלת כאן ולא בפרודקשן.
//
// ההבדל היחיד: כתובת ה-API של הפרודקשן ב-CSP מוחלפת בכתובת ה-API המקומי, שאליו הבנייה
// הזו מדברת (environment.e2e.ts).
import { createServer } from 'node:http';
import { existsSync, readFileSync, statSync } from 'node:fs';
import { extname, join, normalize, resolve } from 'node:path';

const PRODUCTION_API = 'https://talmidon.onrender.com';
const LOCAL_API = 'http://localhost:5208';
// הכתובת שה-API מתיר ב-CORS בסביבת הפיתוח
const PORT = 4200;

const frontend = resolve(import.meta.dirname, '..');
const root = join(frontend, 'dist', 'talmidon', 'browser');
const rule = JSON.parse(readFileSync(join(frontend, 'vercel.json'), 'utf-8')).headers.find(h => h.source === '/(.*)');
const headers = rule.headers.map(({ key, value }) => [key, value.replaceAll(PRODUCTION_API, LOCAL_API)]);

const types = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.ico': 'image/x-icon',
  '.json': 'application/json',
  '.woff2': 'font/woff2',
  '.txt': 'text/plain; charset=utf-8'
};

createServer((request, response) => {
  const path = normalize(decodeURIComponent(new URL(request.url, 'http://localhost').pathname));
  let file = join(root, path);
  // כמו ה-rewrite ב-vercel.json: כל כתובת שאינה קובץ היא ניתוב של Angular
  if (!file.startsWith(root) || !existsSync(file) || statSync(file).isDirectory()) file = join(root, 'index.html');

  for (const [key, value] of headers) response.setHeader(key, value);
  response.setHeader('Content-Type', types[extname(file)] ?? 'application/octet-stream');
  response.end(readFileSync(file));
}).listen(PORT, () => console.log(`e2e site on http://localhost:${PORT}`));
