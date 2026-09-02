import { ChangeDetectionStrategy, Component, computed, input, model, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AutoCompleteModule, AutoCompleteSelectEvent } from 'primeng/autocomplete';

/**
 * בורר תחומי לימוד: שדה חיפוש עם השלמה אוטומטית, התגיות שנבחרו מתחתיו, ורשת
 * הצעות לחיצות שתמיד גלויה.
 *
 * ההצעות הגלויות הן העיקר: הן חוסכות הקלדה ומראות מה מקובל לכתוב, וזה מה
 * שמייצר עקביות בין מורות (ולכן סינון שימושי בספרייה) בלי לכפות רשימה סגורה —
 * כל תחום שלא ברשימה נכנס בהקלדה והקשת Enter.
 */
@Component({
  selector: 'app-subject-picker',
  imports: [FormsModule, AutoCompleteModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p-autocomplete
      [inputId]="inputId()"
      [(ngModel)]="query"
      [suggestions]="filtered()"
      (completeMethod)="filter($event)"
      (onSelect)="pick($event)"
      (keydown.enter)="addTyped($event)"
      [completeOnFocus]="true"
      [showEmptyMessage]="false"
      [placeholder]="placeholder()"
      styleClass="w-full"
      inputStyleClass="w-full" />

    @if (selected().length > 0) {
      <div class="chip-row mt-2">
        @for (name of selected(); track name) {
          <button type="button" class="subject-chip" (click)="remove(name)" [attr.aria-label]="'הסרת ' + name">
            {{ name }}
            <i class="pi pi-times"></i>
          </button>
        }
      </div>
    }

    @if (quickPicks().length > 0) {
      <div class="mt-3">
        <span class="subject-suggest-label">{{ suggestLabel() }}</span>
        <div class="chip-row mt-2">
          @for (name of quickPicks(); track name) {
            <button type="button" class="subject-chip subject-chip-add" (click)="add(name)">
              <i class="pi pi-plus"></i>
              {{ name }}
            </button>
          }
        </div>
      </div>
    }
  `
})
export class SubjectPickerComponent {
  /** התחומים שנבחרו. דו-כיווני — ההורה מקבל כל שינוי. */
  readonly selected = model.required<string[]>();
  /** מאגר ההצעות המלא (קטלוג + מה שמורות אחרות הזינו). */
  readonly suggestions = input<readonly string[]>([]);
  readonly placeholder = input('למשל: מתמטיקה');
  readonly suggestLabel = input('הצעות נפוצות:');
  readonly inputId = input<string | undefined>(undefined);
  /** כמה הצעות להציג ברשת הלחיצה. */
  readonly quickPickCount = input(12);

  protected query = '';
  protected readonly filtered = signal<string[]>([]);

  /** מה שעדיין לא נבחר — משמש גם לרשימה הנפתחת וגם לרשת הלחיצה. */
  private readonly available = computed(() => {
    const chosen = new Set(this.selected().map(n => n.toLowerCase()));
    return this.suggestions().filter(name => !chosen.has(name.toLowerCase()));
  });

  protected readonly quickPicks = computed(() => this.available().slice(0, this.quickPickCount()));

  protected filter(event: { query: string }): void {
    const query = event.query.trim().toLowerCase();
    this.filtered.set(this.available().filter(name => !query || name.toLowerCase().includes(query)).slice(0, 20));
  }

  protected pick(event: AutoCompleteSelectEvent): void {
    this.add(String(event.value));
  }

  /**
   * Enter מוסיף את מה שהוקלד גם כשאינו ברשימה — זה מה שהופך את המאגר להצעות
   * ולא לרשימה סגורה. ‎preventDefault‎ כדי שהטופס שמסביב לא יישלח.
   */
  protected addTyped(event: Event): void {
    event.preventDefault();
    this.add(this.query);
  }

  protected add(name: string): void {
    const clean = name.trim();
    if (!clean) return;
    if (this.selected().some(n => n.toLowerCase() === clean.toLowerCase())) {
      this.query = '';
      return;
    }
    this.selected.set([...this.selected(), clean]);
    this.query = '';
  }

  protected remove(name: string): void {
    this.selected.set(this.selected().filter(n => n !== name));
  }
}
