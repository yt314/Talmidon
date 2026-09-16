import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  template: `
    @if (bannerVisible) {
      <div class="branch-protection-banner" role="status" aria-live="polite">
        <div class="banner-icon" aria-hidden="true">
          <svg viewBox="0 0 16 16" focusable="false">
            <path
              d="M8.5 1.25a2.75 2.75 0 0 1 2.75 2.75v.75h.5a1.5 1.5 0 0 1 1.5 1.5v5.5a1.5 1.5 0 0 1-1.5 1.5H3.75A1.5 1.5 0 0 1 2.25 10.75V6.25a1.5 1.5 0 0 1 1.5-1.5h.5V4A2.75 2.75 0 0 1 7 1.25zm1.25 3.5V4A1.25 1.25 0 0 0 8.5 2.75a1.25 1.25 0 0 0-1.25 1.25v.75zm-3.5 0v.75h3.5V4.75zm-1.5 2.5h6.5v4.5h-6.5z"
              fill="currentColor"/>
          </svg>
        </div>

        <div class="banner-copy">
          <h2>Your main branch isn't protected</h2>
          <p>
            Protect this branch from force pushing or deletion, or require status checks before merging. View
            <a href="https://docs.github.com" target="_blank" rel="noreferrer">documentation.</a>
          </p>
        </div>

        <div class="banner-actions">
          <button type="button" class="banner-button secondary" (click)="dismissBanner()">Dismiss</button>
          <button type="button" class="banner-button primary" (click)="protectBranch()">Protect this branch</button>
        </div>
      </div>
    }

    <router-outlet />
  `
})
export class App {
  bannerVisible = true;

  dismissBanner(): void {
    this.bannerVisible = false;
  }

  protectBranch(): void {
    this.bannerVisible = false;
  }
}
