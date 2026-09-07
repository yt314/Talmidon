import { DOCUMENT, Component, ChangeDetectionStrategy, HostListener, computed, effect, inject, signal, viewChild, ElementRef } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TooltipModule } from 'primeng/tooltip';
import { StudentsService } from '../../features/students/students.service';
import { getInitials, getAvatarColor } from '../avatar/avatar.util';

interface QuickResult {
  label: string;
  hint: string | null;
  icon: string;
  path: string;
  /** תלמידה מקבלת עיגול ראשי תיבות; מסך מקבל אייקון. */
  initials: string | null;
  color: string | null;
}

const SCREENS: { label: string; icon: string; path: string; keywords: string }[] = [
  { label: 'ראשי', icon: 'pi-home', path: '/app/dashboard', keywords: 'דשבורד לוח בקרה' },
  { label: 'תלמידים', icon: 'pi-users', path: '/app/students', keywords: 'תלמידות' },
  { label: 'יומן', icon: 'pi-calendar', path: '/app/lessons', keywords: 'שיעורים לוח שנה' },
  { label: 'תשלומים', icon: 'pi-wallet', path: '/app/payments', keywords: 'כסף חיובים גבייה' },
  { label: 'דוחות', icon: 'pi-chart-bar', path: '/app/reports', keywords: 'הכנסות סיכום' },
  { label: 'פניות', icon: 'pi-inbox', path: '/app/contact-requests', keywords: 'לידים הורים' },
  { label: 'פרופיל ציבורי', icon: 'pi-id-card', path: '/app/profile', keywords: 'הגדרות כרטיס ספרייה' },
  { label: 'חשבון וסיסמה', icon: 'pi-lock', path: '/app/account', keywords: 'הגדרות סיסמה' }
];

/**
 * חיפוש מהיר בכל המערכת — תלמידות ומסכים — מתוך ‎Ctrl+K‎ בכל מסך.
 *
 * התלמידות נטענות פעם אחת בפתיחה הראשונה ולא בעליית המעטפת: רוב הכניסות
 * למערכת אינן נוגעות בחלון הזה, ואין סיבה לשלם עליו קריאה בכל טעינה.
 */
@Component({
  selector: 'app-quick-search',
  imports: [DialogModule, InputTextModule, TooltipModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <!--
      בלי תווית הקיצור: הסרגל ברוחב קבוע, והצ'יפ דחף את התפריט לשורה שנייה.
      הקיצור מופיע בטולטיפ ובתוך החלון עצמו.
    -->
    <button
      type="button"
      class="quick-search-trigger"
      (click)="open()"
      aria-label="חיפוש מהיר"
      pTooltip="חיפוש מהיר · Ctrl+K"
      tooltipPosition="bottom">
      <i class="pi pi-search"></i>
      <span class="quick-search-trigger-label">חיפוש</span>
    </button>

    <!-- נבנה רק כשהוא פתוח: רשימת התוצאות אינה מרונדרת ברקע בכל מסך במערכת -->
    @if (visible()) {
    <p-dialog
      [visible]="true"
      (visibleChange)="visible.set($event)"
      [modal]="true"
      [draggable]="false"
      [showHeader]="false"
      [dismissableMask]="true"
      appendTo="body"
      styleClass="quick-search-dialog"
      [style]="{ width: '32rem' }">
      <div class="quick-search-field">
        <i class="pi pi-search"></i>
        <input
          #box
          pInputText
          type="text"
          placeholder="חיפוש תלמידה או מסך…"
          [value]="term()"
          (input)="term.set($any($event.target).value)"
          (keydown)="onKey($event)" />
      </div>

      <div class="quick-search-hintbar">
        <span><kbd>↑</kbd><kbd>↓</kbd> לניווט</span>
        <span><kbd>Enter</kbd> לפתיחה</span>
        <span><kbd>Esc</kbd> לסגירה</span>
      </div>

      @if (results().length === 0) {
        <p class="quick-search-empty">לא נמצאה התאמה.</p>
      } @else {
        <ul class="quick-search-list">
          @for (item of results(); track item.path; let i = $index) {
            <li>
              <a
                [routerLink]="item.path"
                [class.is-active]="i === active()"
                (click)="visible.set(false)"
                (mouseenter)="active.set(i)">
                @if (item.initials; as initials) {
                  <span class="quick-search-avatar" [style.background]="item.color">{{ initials }}</span>
                } @else {
                  <span class="quick-search-icon"><i class="pi {{ item.icon }}"></i></span>
                }
                <span class="quick-search-label">{{ item.label }}</span>
                @if (item.hint; as hint) {
                  <span class="quick-search-hint">{{ hint }}</span>
                }
              </a>
            </li>
          }
        </ul>
      }
    </p-dialog>
    }
  `
})
export class QuickSearchComponent {
  private readonly students = inject(StudentsService);
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);
  private readonly box = viewChild<ElementRef<HTMLInputElement>>('box');

  protected readonly visible = signal(false);
  protected readonly term = signal('');
  protected readonly active = signal(0);
  private readonly studentRows = signal<QuickResult[]>([]);
  private loaded = false;

  protected readonly results = computed(() => {
    const term = this.term().trim().toLowerCase();
    const screens = SCREENS.filter(
      s => !term || s.label.toLowerCase().includes(term) || s.keywords.includes(term)
    ).map<QuickResult>(s => ({ label: s.label, hint: 'מסך', icon: s.icon, path: s.path, initials: null, color: null }));
    const students = this.studentRows().filter(s => !term || s.label.toLowerCase().includes(term));
    // התלמידות קודם כשמחפשים בפועל; בלי חיפוש הרשימה היא ניווט מהיר
    return (term ? [...students, ...screens] : [...screens, ...students]).slice(0, 8);
  });

  constructor() {
    // כל הקלדה מחזירה את הסימון לתוצאה הראשונה, אחרת Enter מוביל לשורה ישנה
    effect(() => {
      this.term();
      this.active.set(0);
    });
  }

  @HostListener('document:keydown', ['$event'])
  protected onGlobalKey(event: KeyboardEvent): void {
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
      event.preventDefault();
      this.open();
    }
  }

  protected open(): void {
    this.term.set('');
    this.visible.set(true);
    this.load();
    // תוכן הדיאלוג נבנה מחדש בכל פתיחה, ולכן גם ההתמקדות וגם איפוס השדה
    // מחכים לרינדור. השדה מתאפס במפורש: המצב הקודם נשאר בו אחרת, והחיפוש
    // הבא היה מסונן לפי מה שהוקלד בפעם הקודמת.
    setTimeout(() => {
      const el = this.box()?.nativeElement;
      if (!el) return;
      el.value = '';
      el.focus();
    }, 60);
  }

  protected onKey(event: KeyboardEvent): void {
    const items = this.results();
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.active.set((this.active() + 1) % items.length);
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.active.set((this.active() - 1 + items.length) % items.length);
    } else if (event.key === 'Enter') {
      const target = items[this.active()];
      if (!target) return;
      event.preventDefault();
      this.visible.set(false);
      this.router.navigateByUrl(target.path);
    } else if (event.key === 'Escape') {
      this.visible.set(false);
    }
  }

  private load(): void {
    if (this.loaded) return;
    this.loaded = true;
    this.students.list().subscribe({
      next: rows =>
        this.studentRows.set(
          rows.map(s => ({
            label: s.fullName,
            hint: s.gradeLevel,
            icon: 'pi-user',
            path: `/app/students/${s.id}`,
            initials: getInitials(s.fullName),
            color: getAvatarColor(s.fullName)
          }))
        ),
      // כשל בטעינה משאיר את המסכים לחיפוש; אין טעם להטריד על כך
      error: () => (this.loaded = false)
    });
  }
}
