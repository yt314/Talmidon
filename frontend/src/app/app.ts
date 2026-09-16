import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  template: `
    <div class="github-pr-page">
      <header class="github-pr-header">
        <div class="github-pr-header__row">
          <span class="github-state-badge">Open</span>
          <span class="github-pr-title">Add branch protection banner component with styles and functionality</span>
          <span class="github-pr-meta">#35</span>
          <span class="github-pr-meta">yt314 wants to merge 1 commit into</span>
          <span class="github-branch-link">main</span>
          <span class="github-pr-meta">from</span>
          <span class="github-branch-link">profile-city-and-contact</span>
        </div>
      </header>

      <main class="github-pr-main">
        <section class="github-pr-panel">
          <div class="status-row status-row--success">
            <div class="status-row__icon" aria-hidden="true">
              <svg viewBox="0 0 16 16" fill="none">
                <path d="M8 1.2a6.8 6.8 0 1 0 0 13.6A6.8 6.8 0 0 0 8 1.2Zm3.2 5.1-3.7 4.4a.7.7 0 0 1-1.06 0L4.7 9.1a.7.7 0 1 1 1.06-1.06l1.57 1.57L10.1 5.2a.7.7 0 1 1 1.1 1.1Z" fill="currentColor"/>
              </svg>
            </div>
            <div class="status-row__content">
              <div class="status-row__title">This branch was successfully deployed</div>
              <div class="status-row__meta">1 active deployment</div>
            </div>
            <button type="button" class="ghost-button">Show environments</button>
          </div>

          <div class="status-row status-row--pending">
            <div class="status-row__icon" aria-hidden="true">
              <svg viewBox="0 0 16 16" fill="none">
                <path d="M8 15a7 7 0 1 1 0-14 7 7 0 0 1 0 14Zm0-1.2A5.8 5.8 0 1 0 8 2.2a5.8 5.8 0 0 0 0 11.6Zm.7-9.9H7.3v4.2L10.8 11l.9-.9-2.9-2.9V4.1Z" fill="currentColor"/>
              </svg>
            </div>
            <div class="status-row__content">
              <div class="status-row__title">Checks awaiting conflict resolution</div>
              <div class="status-row__meta">2 successful checks</div>
            </div>
            <button type="button" class="status-row__chevron" aria-label="Open checks">›</button>
          </div>

          <div class="status-row status-row--warning">
            <div class="status-row__icon" aria-hidden="true">
              <svg viewBox="0 0 16 16" fill="none">
                <path d="M8 1.2a1 1 0 0 1 .87.5l5.9 10.2a1 1 0 0 1-.87 1.5H2.1A1 1 0 0 1 1.2 12L7.1 1.7A1 1 0 0 1 8 1.2Zm.05 4.5h-1.1v4.2h1.1V5.7Zm-.55 5.8a.68.68 0 1 0 1.35 0 .68.68 0 0 0-1.35 0Z" fill="currentColor"/>
              </svg>
            </div>
            <div class="status-row__content">
              <div class="status-row__title">This branch has conflicts that must be resolved</div>
              <div class="status-row__meta">Use the web editor or the command line to resolve conflicts before continuing.</div>
              <div class="status-row__command-row">
                <div class="status-row__command-box">
                  <svg viewBox="0 0 16 16" fill="none">
                    <path d="M2.5 3.3A1.3 1.3 0 0 1 3.8 2h8.4a1.3 1.3 0 0 1 1.3 1.3v9.4A1.3 1.3 0 0 1 12.2 14H3.8A1.3 1.3 0 0 1 2.5 12.7V3.3Zm1.3-.3h7.4v8.9H3.8V3Zm1.1 6.6h5.2v1H4.9v-1Zm0-2.2h5.2v1H4.9v-1Z" fill="currentColor"/>
                  </svg>
                  <span>frontend/src/app/app.ts</span>
                </div>
              </div>
            </div>
            <button type="button" class="primary-button">Resolve conflicts</button>
          </div>

          <div class="merge-row">
            <button type="button" class="secondary-button">Merge pull request</button>
            <button type="button" class="merge-arrow" aria-label="More merge options">▼</button>
            <span class="merge-row__hint">You can also merge this with the command line. <a href="https://docs.github.com" target="_blank" rel="noreferrer">View command line instructions.</a></span>
          </div>
        </section>

        <aside class="github-pr-sidebar">
          <div class="sidebar-panel">
            <div class="sidebar-panel__title">Development</div>
            <div class="sidebar-panel__body">
              Successfully merging this pull request may close these issues.
            </div>
            <div class="sidebar-panel__note">None yet</div>
          </div>

          <div class="sidebar-panel sidebar-panel--light">
            <div class="sidebar-panel__header">
              <span>Notifications</span>
              <button type="button" class="link-button">Customize</button>
            </div>
            <button type="button" class="notification-toggle">
              <span class="notification-toggle__icon">✦</span>
              Unsubscribe
            </button>
            <div class="sidebar-panel__body sidebar-panel__body--small">You're receiving notifications because you authored the thread.</div>
          </div>

          <div class="sidebar-panel sidebar-panel--light">
            <div class="sidebar-panel__item">1 participant</div>
            <div class="sidebar-panel__avatar" aria-label="Participant avatar"></div>
          </div>

          <div class="sidebar-panel sidebar-panel--light sidebar-panel--list">
            <button type="button" class="list-button">Lock conversation</button>
            <button type="button" class="list-button">Archive pull request</button>
          </div>
        </aside>
      </main>

      <div class="comment-block">
        <div class="comment-avatar"></div>
        <div class="comment-editor">
          <div class="comment-editor__header">
            <span class="comment-title">Add a comment</span>
          </div>
          <div class="editor-tabs">
            <button type="button" class="tab active">Write</button>
            <button type="button" class="tab">Preview</button>
          </div>
          <div class="editor-toolbar">
            <span>H</span>
            <span>B</span>
            <span>I</span>
            <span>≡</span>
            <span>•</span>
            <span>☰</span>
            <span>🔗</span>
            <span>📎</span>
            <span>🙂</span>
            <span>✎</span>
            <span>◌</span>
          </div>
          <textarea placeholder="Add your comment here..."></textarea>
        </div>
      </div>
    </div>

    <router-outlet />
  `
})
export class App {}
