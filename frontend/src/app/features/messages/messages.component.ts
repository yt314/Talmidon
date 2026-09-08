import { Component, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectButtonModule } from 'primeng/selectbutton';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { extractErrorMessage } from '../../core/http/extract-error-message';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { ConversationComponent } from './conversation.component';
import { MessageAuthor, MessageRecipient, ThreadDetail, ThreadSummary } from './messages.models';
import { MessagesService } from './messages.service';
import { ThreadListComponent } from './thread-list.component';

/** נמען עם התווית המוכנה — p-select מקבל שם שדה, לא פונקציה. */
type RecipientOption = MessageRecipient & { label: string };

/** התיבה של המורה: כל השיחות מול התלמידות וההורים במקום אחד. */
@Component({
  selector: 'app-messages',
  imports: [
    FormsModule,
    ButtonModule,
    DialogModule,
    InputTextModule,
    SelectModule,
    SelectButtonModule,
    TextareaModule,
    EmptyStateComponent,
    PageHeaderComponent,
    ThreadListComponent,
    ConversationComponent
  ],
  templateUrl: './messages.component.html',
  styleUrls: ['./messages.scss', './messages-screen.scss']
})
export class MessagesComponent implements OnInit {
  private readonly service = inject(MessagesService);
  private readonly toast = inject(MessageService);
  private readonly route = inject(ActivatedRoute);

  private readonly conversation = viewChild(ConversationComponent);

  protected readonly Author = MessageAuthor;

  protected readonly loading = signal(true);
  protected readonly threads = signal<ThreadSummary[]>([]);
  protected readonly selected = signal<ThreadDetail | null>(null);
  protected readonly sending = signal(false);
  /** false = רק שיחות פתוחות. ברירת המחדל היא הכול, כדי שלא ייעלם מידע בלי שביקשו. */
  protected readonly showClosed = signal(true);

  protected readonly filterOptions = [
    { label: 'הכול', value: true },
    { label: 'פתוחות', value: false }
  ];

  protected readonly visible = computed(() =>
    this.showClosed() ? this.threads() : this.threads().filter(t => !t.isClosed)
  );

  // ----- שיחה חדשה -----
  protected readonly composeOpen = signal(false);
  protected readonly recipients = signal<RecipientOption[]>([]);
  protected readonly recipient = signal<RecipientOption | null>(null);
  protected readonly subject = signal('');
  protected readonly body = signal('');

  /** "רותם · בעניין דנה" — שורה אחת שמסבירה בדיוק למי ההודעה הולכת. */
  private static label(r: MessageRecipient): string {
    return r.role === MessageAuthor.Student ? r.name : `${r.name} · בעניין ${r.studentName}`;
  }

  ngOnInit(): void {
    // התראה על הודעה מפנה לכאן עם מזהה השיחה, כדי לפתוח בדיוק את מה שהתריע
    const threadId = this.route.snapshot.queryParamMap.get('thread');
    this.load(threadId);
  }

  protected open(thread: ThreadSummary): void {
    this.service.thread(thread.id).subscribe({
      next: detail => {
        this.selected.set(detail);
        // הסימון כנקרא קורה בשרת בעת הפתיחה — מיישרים כאן את הרשימה בלי לטעון אותה שוב
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
    this.service.teacherReply(thread.id, body).subscribe({
      next: message => {
        this.sending.set(false);
        this.conversation()?.clearDraft();
        this.selected.set({ ...thread, isClosed: false, messages: [...thread.messages, message] });
        this.refreshRow(thread.id, message.body, MessageAuthor.Teacher, message.createdAt);
      },
      error: err => {
        this.sending.set(false);
        this.fail(err, 'ההודעה לא נשלחה.');
      }
    });
  }

  protected setClosed(closed: boolean): void {
    const thread = this.selected();
    if (!thread) return;

    this.service.setClosed(thread.id, closed).subscribe({
      next: () => {
        this.selected.set({ ...thread, isClosed: closed });
        this.threads.update(list => list.map(t => (t.id === thread.id ? { ...t, isClosed: closed } : t)));
      },
      error: err => this.fail(err, 'העדכון נכשל.')
    });
  }

  protected openCompose(): void {
    this.composeOpen.set(true);
    if (this.recipients().length === 0) {
      this.service.recipients().subscribe({
        next: list => this.recipients.set(list.map(r => ({ ...r, label: MessagesComponent.label(r) }))),
        error: err => this.fail(err, 'לא הצלחנו לטעון את רשימת הנמענים.')
      });
    }
  }

  protected startThread(): void {
    const to = this.recipient();
    const subject = this.subject().trim();
    const body = this.body().trim();
    if (!to || !subject || !body) return;

    this.sending.set(true);
    this.service
      .startThread({
        studentId: to.studentId,
        counterpartRole: to.role,
        counterpartId: to.id,
        subject,
        body
      })
      .subscribe({
        next: detail => {
          this.sending.set(false);
          this.composeOpen.set(false);
          this.subject.set('');
          this.body.set('');
          this.recipient.set(null);
          this.selected.set(detail);
          this.load();
          this.toast.add({ severity: 'success', summary: 'נשלח', detail: 'ההודעה נשלחה.' });
        },
        error: err => {
          this.sending.set(false);
          this.fail(err, 'ההודעה לא נשלחה.');
        }
      });
  }

  private load(openId: string | null = null): void {
    this.loading.set(true);
    this.service.inbox().subscribe({
      next: list => {
        this.threads.set(list);
        this.loading.set(false);
        if (openId) {
          const match = list.find(t => t.id === openId);
          if (match) this.open(match);
        }
      },
      error: () => this.loading.set(false)
    });
  }

  /** מעדכן את שורת השיחה ברשימה ומעלה אותה למעלה, בלי לטעון את התיבה מחדש. */
  private refreshRow(id: string, preview: string, sender: MessageAuthor, at: string): void {
    this.threads.update(list => {
      const updated = list.map(t =>
        t.id === id
          ? { ...t, lastMessagePreview: preview, lastSenderRole: sender, lastMessageAt: at, hasUnread: false, isClosed: false }
          : t
      );
      const moved = updated.find(t => t.id === id);
      return moved ? [moved, ...updated.filter(t => t.id !== id)] : updated;
    });
  }

  private fail(err: unknown, fallback: string): void {
    this.toast.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, fallback) });
  }
}
