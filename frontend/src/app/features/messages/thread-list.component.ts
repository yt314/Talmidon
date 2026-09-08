import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { MessageAuthor, ThreadSummary } from './messages.models';

/**
 * רשימת השיחות. אותה רשימה משרתת את המורה ואת מי שמדבר איתה, ומה שמשתנה הוא רק
 * מי מוצג ככותרת השורה: אצל המורה — הצד השני, ואצל התלמידה או ההורה — המורה.
 */
@Component({
  selector: 'app-thread-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe],
  templateUrl: './thread-list.component.html',
  styleUrl: './messages.scss'
})
export class ThreadListComponent {
  readonly threads = input.required<ThreadSummary[]>();
  readonly selectedId = input<string | null>(null);
  /** 'teacher' — התיבה של המורה; 'portal' — התיבה של התלמידה או ההורה. */
  readonly perspective = input<'teacher' | 'portal'>('teacher');

  readonly select = output<ThreadSummary>();

  protected readonly Author = MessageAuthor;

  protected readonly isTeacherView = computed(() => this.perspective() === 'teacher');

  protected title(thread: ThreadSummary): string {
    return this.isTeacherView() ? thread.counterpartName : 'המורה';
  }

  /**
   * שורת ההקשר מתחת לשם. אצל המורה היא מציינת בשם מי מדובר — אבל רק כשזה לא אותו אדם,
   * כדי ש"רותם / רותם" לא יופיע בכל שורה שנייה.
   */
  protected context(thread: ThreadSummary): string | null {
    if (!this.isTeacherView()) return thread.subject;
    return thread.counterpartRole === MessageAuthor.Parent ? `בעניין ${thread.studentName}` : thread.subject;
  }
}
