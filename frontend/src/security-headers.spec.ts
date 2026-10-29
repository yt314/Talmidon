import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { environment } from './environments/environment.production';

/**
 * כותרות האבטחה של האתר חיות בשני קבצי תצורה (vercel.json לפריסה המתארחת, Caddyfile
 * לפריסה העצמית), ומדיניות ה-CSP שבהן תלויה בקוד: גיבוב של הסקריפט שב-index.html,
 * וכתובת ה-API. שינוי באחד מהם בלי השני לא נכשל בשום בנייה — הדפדפן פשוט חוסם בשקט
 * את מה שלא הותר, והאתר נשבר רק בפרודקשן. הבדיקות כאן תופסות את זה מוקדם.
 */
const read = (file: string) => readFileSync(resolve(process.cwd(), file), 'utf-8');

interface VercelConfig {
  headers: { source: string; headers: { key: string; value: string }[] }[];
}

const vercelHeaders = (): Map<string, string> => {
  const config = JSON.parse(read('vercel.json')) as VercelConfig;
  const all = config.headers.find(rule => rule.source === '/(.*)');
  return new Map((all?.headers ?? []).map(header => [header.key, header.value]));
};

const caddyHeaders = (): Map<string, string> => {
  const block = /header\s*\{([^}]*)\}/.exec(read('Caddyfile'))?.[1] ?? '';
  const lines = [...block.matchAll(/^\s*\?([\w-]+)\s+"([^"]*)"/gm)];
  return new Map(lines.map(([, key, value]) => [key, value]));
};

/** index.html כפי שהדפדפן קורא אותו — כדי לא לפספס סקריפט עם מאפיינים או באותיות גדולות. */
const indexDocument = () => new DOMParser().parseFromString(read('src/index.html'), 'text/html');

const csp = () => vercelHeaders().get('Content-Security-Policy') ?? '';

const directive = (name: string) =>
  csp().split(';').map(part => part.trim().split(/\s+/)).find(([key]) => key === name)?.slice(1) ?? [];

describe('security headers', () => {
  it('Caddyfile קובע בדיוק את אותן כותרות כמו vercel.json', () => {
    expect(caddyHeaders()).toEqual(vercelHeaders());
  });

  it('כל סקריפט מוטבע ב-index.html מותר ב-CSP לפי הגיבוב שלו', () => {
    const scripts = [...indexDocument().querySelectorAll('script:not([src])')].map(script => script.textContent ?? '');
    expect(scripts.length).toBeGreaterThan(0);

    for (const body of scripts) {
      const hash = createHash('sha256').update(body, 'utf-8').digest('base64');
      expect(directive('script-src')).toContain(`'sha256-${hash}'`);
    }
  });

  it('ה-CSP מתיר את ה-API: קריאות, ותמונות הפרופיל שמוגשות ממנו', () => {
    const apiOrigin = new URL(environment.apiUrl).origin;
    expect(directive('connect-src')).toContain(apiOrigin);
    expect(directive('img-src')).toContain(apiOrigin);
  });

  it('אין ב-index.html מטפלי אירועים מוטבעים, וגם הבנייה לא מוסיפה כאלה', () => {
    // CSP בלי 'unsafe-inline' חוסם onload="..." ודומיו. הטמעת ה-CSS הקריטי של Angular
    // טוענת את גיליון הסגנונות דרך onload="this.media='all'", ולכן היא כבויה ב-angular.json —
    // אחרת האתר כולו היה עולה בלי עיצוב.
    const handlers = [...indexDocument().querySelectorAll('*')]
      .flatMap(element => [...element.attributes].map(attribute => attribute.name))
      .filter(name => name.startsWith('on'));
    expect(handlers).toEqual([]);

    const angular = JSON.parse(read('angular.json'));
    const project = Object.values(angular.projects)[0] as {
      architect: { build: { configurations: { production: { optimization: { styles: { inlineCritical: boolean } } } } } };
    };
    expect(project.architect.build.configurations.production.optimization.styles.inlineCritical).toBe(false);
  });

  it('האתר אינו ניתן להטמעה באתר אחר', () => {
    expect(directive('frame-ancestors')).toEqual(["'none'"]);
    expect(vercelHeaders().get('X-Frame-Options')).toBe('DENY');
  });
});
