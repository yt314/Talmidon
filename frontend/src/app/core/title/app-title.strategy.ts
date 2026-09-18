import { Injectable, inject } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';

const APP_NAME = 'תלמידון';

/**
 * Keeps the document title in step with the route.
 *
 * In a single-page app the title does not change by itself, so every screen
 * showed up in the browser tab and in history under the same name. For anyone
 * using a screen reader it is also the only announcement that says "you have
 * moved", since there is no page load to say it.
 *
 * Each route's title matches the heading shown on the screen, so the tab and
 * the page say the same thing.
 */
@Injectable({ providedIn: 'root' })
export class AppTitleStrategy extends TitleStrategy {
  private readonly title = inject(Title);

  override updateTitle(snapshot: RouterStateSnapshot): void {
    const page = this.buildTitle(snapshot);
    this.title.setTitle(page ? `${page} · ${APP_NAME}` : APP_NAME);
  }
}
