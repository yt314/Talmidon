import { vi } from 'vitest';

/** jsdom אינו מממש IntersectionObserver, שעליו נשענת החשיפה בגלילה (RevealDirective). */
export function stubIntersectionObserver(): void {
  vi.stubGlobal(
    'IntersectionObserver',
    class {
      observe = vi.fn();
      unobserve = vi.fn();
      disconnect = vi.fn();
      takeRecords = vi.fn(() => []);
    }
  );
}
