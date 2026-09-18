import { DOCUMENT, Directive, ElementRef, HostListener, Injectable, inject } from '@angular/core';

/**
 * Remembers where focus last went, so a closing dialog knows where to send it
 * back to.
 *
 * One listener for the whole app, registered the first time any dialog asks
 * for it. It keeps a short history rather than only the last element, so a
 * dialog opened from inside another dialog can find what was focused before
 * it, not what was focused before both.
 */
@Injectable({ providedIn: 'root' })
export class DialogFocusHistory {
  private readonly doc = inject(DOCUMENT);
  private readonly history: HTMLElement[] = [];
  private listening = false;

  start(): void {
    if (this.listening) return;
    this.listening = true;
    // Capture phase: focusin does bubble, but we want to see it even where
    // something stops it on the way up.
    this.doc.addEventListener(
      'focusin',
      (event) => {
        const el = event.target as HTMLElement | null;
        if (!el || typeof el.focus !== 'function') return;
        this.history.push(el);
        if (this.history.length > 5) this.history.shift();
      },
      true,
    );
  }

  /** The most recent element focused outside `container`, if it is still in the document. */
  lastOutside(container: HTMLElement): HTMLElement | null {
    for (let i = this.history.length - 1; i >= 0; i--) {
      const el = this.history[i];
      if (el.isConnected && !container.contains(el)) return el;
    }
    return null;
  }
}

/**
 * Returns focus to whatever opened the dialog, once the dialog closes.
 *
 * p-dialog moves focus into itself on open (focusOnShow) but has no setting at
 * all for putting it back on close. The result is that anyone working by
 * keyboard loses their place: after Escape focus is back on <body> and they
 * have to tab from the top of the page again. Mouse users never notice.
 *
 * The selector is p-dialog itself, so every dialog in a component that imports
 * this directive gets the behaviour without touching its template.
 */
@Directive({ selector: 'p-dialog' })
export class RestoreFocusOnCloseDirective {
  private readonly doc = inject(DOCUMENT);
  private readonly host = inject(ElementRef).nativeElement as HTMLElement;
  private readonly focusHistory = inject(DialogFocusHistory);
  private trigger: HTMLElement | null = null;

  constructor() {
    this.focusHistory.start();
  }

  @HostListener('onShow')
  protected rememberTrigger(): void {
    this.trigger = this.focusHistory.lastOutside(this.host);
  }

  @HostListener('onHide')
  protected returnFocus(): void {
    const target = this.trigger;
    this.trigger = null;
    if (!target) return;

    // A close still has an animation and a removal from the DOM to get
    // through; wait for those before moving focus.
    setTimeout(() => {
      // The element that opened the dialog is gone — after a delete, for
      // instance. Leaving focus where it is beats jumping somewhere arbitrary.
      if (!target.isConnected) return;
      // Someone has already moved focus somewhere real — do not take it away.
      const active = this.doc.activeElement;
      if (active && active !== this.doc.body && this.doc.contains(active)) return;
      target.focus();
    });
  }
}
