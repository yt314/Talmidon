import { Component, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { AuthService } from '../../core/auth/auth.service';
import { extractErrorMessage } from '../../core/http/extract-error-message';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { ParentPortalService } from '../parent-portal/parent-portal.service';
import { ConversationComponent } from './conversation.component';
import { MessageAuthor, ThreadDetail, ThreadSummary } from './messages.models';
import { MessagesService } from './messages.service';
import { ThreadListComponent } from './thread-list.component';

/**
 * תיבת ההודעות של התלמידה ושל ההורה — אותו מסך לשניהם.
 *
 * ההבדל היחיד הוא בפתיחת פנייה חדשה: תלמידה פונה בעניין עצמה, והורה בוחר באיזה ילד
 * מדובר. אפשר להגיע לכאן ממסך ההערות עם ‎?noteId=‎, וכך "השב" על הערה הופך לשיחה
 * שהמורה רואה יחד עם ההערה שעליה מגיבים.
 */
@Component({
  selector: 'app-portal-messages',
  imports: [
    FormsModule,
    ButtonModule,
    DialogModule,
    InputTextModule,
    SelectModule,
    TextareaModule,
    EmptyStateComponent,
    PageHeaderComponent,
    ThreadListComponent,
    ConversationComponent
  ],
  templateUrl: './portal-messages.component.html',
  styleUrls: ['./messages.scss', './messages-screen.scss']
})
export class PortalMessagesComponent implements OnInit {
  private readonly service = inject(MessagesService);
  private readonly parentPortal = inject(ParentPortalService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(MessageService);
  private readonly route = inject(ActivatedRoute);

  private readonly conversation = viewChild(ConversationComponent);

  protected readonly isParent = this.auth.hasRole('Parent');
  protected readonly me = this.isParent ? MessageAuthor.Parent : MessageAuthor.Student;

  protected readonly loading = signal(true);
  protected readonly threads = signal<ThreadSummary[]>([]);
  protected readonly selected = signal<ThreadDetail | null>(null);
  protected readonly sending = signal(false);

  // ----- פנייה חדשה -----
  protected readonly composeOpen = signal(false);
  protected readonly children = signal<{ id: string; fullName: string }[]>([]);
  protected readonly childId = signal<string | null>(null);
  protected readonly subject = signal('');
  protected readonly body = signal('');
  /** ההערה שעליה מגיבים, כשהגיעו לכאן ממסך ההערות. */
  protected readonly noteId = signal<string | null>(null);

  protected readonly canSend = computed(
    () =>
      this.subject().trim().length > 0 &&
      this.body().trim().length > 0 &&
      (!this.isParent || this.childId() !== null)
  );

  ngOnInit(): void {
    if (this.isParent) {
      this.parentPortal.myChildren().subscribe(list => {
        this.children.set(list.map(c => ({ id: c.id, fullName: c.fullName })));
        // הורה לילד יחיד לא צריך לבחור — הבחירה כבר נעשתה
        if (list.length === 1 && this.childId() === null) this.childId.set(list[0].id);
      });
    }

    const params = this.route.snapshot.queryParamMap;
    const note = params.get('noteId');
    if (note) {
      this.noteId.set(note);
      this.subject.set(params.get('subject') ?? 'תגובה להערה');
      // מסך ההערות יודע באיזה ילד מדובר, ולכן ההורה לא נשאל על כך שוב
      const child = params.get('studentId');
      if (child) this.childId.set(child);
      this.composeOpen.set(true);
    }

    this.load();
  }

  protected open(thread: ThreadSummary): void {
    this.service.myThread(thread.id).subscribe({
      next: detail => {
        this.selected.set(detail);
        this.threads.update(list => list.map(t => (t.id === detail.id ? { ...t, hasUnread: false } : t)));
      },
      error: err => this.fail(err, 'לא הצלחנו לפתוח את השיחה.')
    });
  }

  protected close(): void {
    this.selected.set(null);
  }

  protected send(body: string): void {
    const thread = this.selected();
    if (!thread) return;

    this.sending.set(true);
    this.service.myReply(thread.id, body).subscribe({
      next: message => {
        this.sending.set(false);
        this.conversation()?.clearDraft();
        this.selected.set({ ...thread, isClosed: false, messages: [...thread.messages, message] });
        this.threads.update(list =>
          list.map(t =>
            t.id === thread.id
              ? {
                  ...t,
                  lastMessagePreview: message.body,
                  lastSenderRole: this.me,
                  lastMessageAt: message.createdAt,
                  hasUnread: false,
                  isClosed: false
                }
              : t
          )
        );
      },
      error: err => {
        this.sending.set(false);
        this.fail(err, 'ההודעה לא נשלחה.');
      }
    });
  }

  protected startThread(): void {
    if (!this.canSend()) return;

    this.sending.set(true);
    this.service
      .startMyThread({
        studentId: this.isParent ? this.childId() : null,
        relatedNoteId: this.noteId(),
        subject: this.subject().trim(),
        body: this.body().trim()
      })
      .subscribe({
        next: detail => {
          this.sending.set(false);
          this.composeOpen.set(false);
          this.subject.set('');
          this.body.set('');
          this.noteId.set(null);
          this.selected.set(detail);
          this.load();
          this.toast.add({ severity: 'success', summary: 'נשלח', detail: 'הפנייה נשלחה למורה.' });
        },
        error: err => {
          this.sending.set(false);
          this.fail(err, 'הפנייה לא נשלחה.');
        }
      });
  }

  private load(): void {
    this.loading.set(true);
    this.service.myThreads().subscribe({
      next: list => {
        this.threads.set(list);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  private fail(err: unknown, fallback: string): void {
    this.toast.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, fallback) });
  }
}
