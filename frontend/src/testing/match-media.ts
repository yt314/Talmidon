import { vi } from 'vitest';

/**
 * jsdom אינו מממש את window.matchMedia, וכל רכיב שמציג את מתג המצב הכהה (ThemeService)
 * או בודק העדפת "פחות תנועה" נופל בלעדיו. בדפדפן אמיתי הוא תמיד קיים.
 */
export function stubMatchMedia(matches = false): void {
  Object.defineProperty(window, 'matchMedia', {
    configurable: true,
    writable: true,
    value: vi.fn((query: string) => ({
      matches,
      media: query,
      onchange: null,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      addListener: vi.fn(),
      removeListener: vi.fn(),
      dispatchEvent: vi.fn(() => false)
    }))
  });
}
