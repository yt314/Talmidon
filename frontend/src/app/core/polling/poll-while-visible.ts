import {
  EMPTY,
  Observable,
  distinctUntilChanged,
  fromEvent,
  map,
  startWith,
  switchMap,
  timer,
} from 'rxjs';

/**
 * A timer that runs only while the tab is being looked at.
 *
 * The unread counters refresh once a minute. A plain `timer` keeps doing that
 * in a tab left open in the background — overnight that is hundreds of
 * requests for a badge nobody is looking at, and on a phone it is data and
 * battery spent on the same.
 *
 * Emits immediately on subscribe and again the moment the tab comes back, so
 * the counter is current when she returns rather than up to a minute stale —
 * which is better than what the plain timer gave her.
 */
export function pollWhileVisible(doc: Document, periodMs: number): Observable<number> {
  return fromEvent(doc, 'visibilitychange').pipe(
    startWith(null),
    map(() => doc.visibilityState === 'visible'),
    distinctUntilChanged(),
    switchMap((visible) => (visible ? timer(0, periodMs) : EMPTY)),
  );
}
