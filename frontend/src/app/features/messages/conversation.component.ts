import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { TextareaModule } from 'primeng/textarea';
import { TooltipModule } from 'primeng/tooltip';
import { MessageAuthor, ThreadDetail } from './messages.models';

/**
 * חלון השיחה: ההודעות לפי סדר, וחלון כתיבה בתחתית.
 *
 * הרכיב אינו יודע מי מדבר — הוא מקבל את זה ב-<code>me</code>, ולפי זה הודעה מוצגת
 * ימינה או שמאלה. כך אותה תצוגה משרתת את שלושת הצדדים.
 */
@Component({
  selector: 'app-conversation',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, FormsModule, ButtonModule, TextareaModule, TooltipModule],
  templateUrl: './conversation.component.html',
  styleUrl: './messages.scss'
})
export class ConversationComponent {
  readonly thread = input.required<ThreadDetail>();
  /** התפקיד של מי שצופה כרגע — לפיו נקבע איזה צד של השיחה הוא "שלי". */
  readonly me = input.required<MessageAuthor>();
  readonly sending = input(false);
  /** סגירה ופתיחה מחדש הן פעולה של המורה בלבד. */
  readonly canClose = input(false);

  readonly send = output<string>();
  readonly back = output<void>();
  readonly toggleClosed = output<boolean>();

  protected readonly draft = signal('');

  protected readonly Author = MessageAuthor;

  /** מול מי אני מדבר/ת — כותרת החלון. */
  protected readonly headline = computed(() =>
    this.me() === MessageAuthor.Teacher ? this.thread().counterpartName : 'המורה'
  );

  /** אצל המורה מוסיפים בעניין מי, כשהצד השני אינו התלמידה עצמה. */
  protected readonly subline = computed(() => {
    const thread = this.thread();
    return this.me() === MessageAuthor.Teacher && thread.counterpartRole === MessageAuthor.Parent
      ? `בעניין ${thread.studentName}`
      : null;
  });

  protected submit(): void {
    const body = this.draft().trim();
    if (!body || this.sending()) return;
    this.send.emit(body);
  }

  /** נקרא מהמסך אחרי שליחה מוצלחת — הרכיב לא מנחש מתי הטיוטה נשמרה. */
  clearDraft(): void {
    this.draft.set('');
  }
}
