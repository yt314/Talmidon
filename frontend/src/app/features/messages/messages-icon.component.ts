import {
  ChangeDetectionStrategy,
  Component,
  DOCUMENT,
  DestroyRef,
  OnInit,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { switchMap } from 'rxjs/operators';
import { pollWhileVisible } from '../../core/polling/poll-while-visible';
import { MessagesService } from './messages.service';

/**
 * מעטפה עם מונה בסרגל העליון. מקומה ליד הפעמון ולא בתפריט: תיבת דואר היא מקום שחוזרים
 * אליו, ותפריט הניווט של המורה כבר צפוף.
 */
@Component({
  selector: 'app-messages-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ButtonModule],
  template: `
    <p-button
      icon="pi pi-comments"
      [rounded]="true"
      [text]="true"
      severity="secondary"
      [badge]="unread() > 0 ? unread().toString() : undefined"
      badgeSeverity="danger"
      ariaLabel="הודעות"
      (onClick)="go()"
    />
  `,
})
export class MessagesIconComponent implements OnInit {
  /** 'teacher' — התיבה של המורה; 'portal' — של התלמידה או ההורה. */
  readonly mode = input.required<'teacher' | 'portal'>();
  /** לאן מנווטים בלחיצה. */
  readonly link = input.required<string>();

  private readonly service = inject(MessagesService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly doc = inject(DOCUMENT);

  protected readonly unread = signal(0);

  private readonly isTeacher = computed(() => this.mode() === 'teacher');

  ngOnInit(): void {
    // Same cadence as the bell: often enough to notice, rare enough not to
    // weigh on the server — and only while the tab is actually being watched.
    pollWhileVisible(this.doc, 60_000)
      .pipe(
        switchMap(() =>
          this.isTeacher() ? this.service.teacherUnreadCount() : this.service.myUnreadCount(),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({ next: (res) => this.unread.set(res.count), error: () => {} });
  }

  protected go(): void {
    this.router.navigateByUrl(this.link());
  }
}
