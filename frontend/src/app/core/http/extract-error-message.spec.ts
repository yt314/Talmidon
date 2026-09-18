import { describe, expect, it } from 'vitest';
import { extractErrorMessage } from './extract-error-message';

const FALLBACK = 'העדכון נכשל.';

describe('extractErrorMessage', () => {
  /**
   * The request never reached the server. A generic "it failed" leaves open
   * whether the change was saved; here the answer is known — it was not, and
   * retrying is worth it.
   */
  it('reports a connection problem when the request never reached the server', () => {
    // What Angular hands us on a network failure: status 0, and a browser
    // event rather than a response body under `error`.
    expect(extractErrorMessage({ status: 0, error: { type: 'error' } }, FALLBACK)).toContain('חיבור');
  });

  it('does not mistake a real server error for a lost connection', () => {
    expect(extractErrorMessage({ status: 409, error: { message: 'השיעור כבר הושלם.' } }, FALLBACK))
      .toBe('השיעור כבר הושלם.');
  });

  it('prefers the message the controller sent', () => {
    expect(extractErrorMessage({ status: 400, error: { message: 'תלמיד לא נמצא.' } }, FALLBACK))
      .toBe('תלמיד לא נמצא.');
  });

  it('joins a list of errors into one sentence', () => {
    expect(extractErrorMessage({ status: 400, error: { errors: ['שדה חסר.', 'ערך לא תקין.'] } }, FALLBACK))
      .toBe('שדה חסר. ערך לא תקין.');
  });

  /** ASP.NET ProblemDetails: `errors` is a map of field to list of messages. */
  it('flattens ASP.NET validation errors, which arrive as a dictionary', () => {
    const body = { errors: { Email: ['כתובת לא תקינה.'], Password: ['קצרה מדי.'] } };
    expect(extractErrorMessage({ status: 400, error: body }, FALLBACK)).toBe('כתובת לא תקינה. קצרה מדי.');
  });

  /** The ProblemDetails `title` is English boilerplate — worse than the Hebrew fallback. */
  it('falls back when the body holds nothing readable', () => {
    expect(extractErrorMessage({ status: 500, error: { title: 'An error occurred' } }, FALLBACK)).toBe(FALLBACK);
    expect(extractErrorMessage(undefined, FALLBACK)).toBe(FALLBACK);
  });
});
