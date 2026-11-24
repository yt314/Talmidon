/**
 * מ-Node 25 ואילך ל-Node יש localStorage ו-sessionStorage משלו, והם לא זמינים אלא אם
 * Node הופעל עם ‎--localstorage-file. Vitest מעתיק מ-jsdom רק גלובלים ש-Node עוד לא
 * הגדיר, ולכן נשארים אלה של Node, וכל בדיקה שנוגעת באחסון נכשלת. כאן הם מוחזרים לאלה של
 * jsdom — אותו אחסון שהדפדפן נותן לאפליקציה.
 */
const dom = (globalThis as { jsdom?: { window: Window } }).jsdom;

if (dom) {
  for (const name of ['localStorage', 'sessionStorage'] as const) {
    Object.defineProperty(globalThis, name, { value: dom.window[name], configurable: true, writable: true });
  }
}
