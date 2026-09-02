import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { getAvatarColor, getInitials } from './avatar.util';

/**
 * תמונת פרופיל: התמונה עצמה כשיש אחת, ואחרת עיגול ראשי תיבות בצבע שנגזר מהשם.
 * ריכוז שתי האפשרויות ברכיב אחד — קודם כל מסך צייר את העיגול בעצמו, וכשנוספה
 * תמונה הייתה צריכה להתווסף בכל מקום בנפרד.
 */
@Component({
  selector: 'app-avatar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (photoUrl()) {
      <img class="avatar-circle avatar-photo" [class]="sizeClass()" [src]="photoUrl()" [alt]="name()" loading="lazy" />
    } @else {
      <span class="avatar-circle" [class]="sizeClass()" [style.--avatar-color]="color()">{{ initials() }}</span>
    }
  `
})
export class AvatarComponent {
  readonly name = input.required<string>();
  readonly photoUrl = input<string | null>(null);
  readonly size = input<'sm' | 'md' | 'lg'>('md');

  protected readonly initials = computed(() => getInitials(this.name()));
  protected readonly color = computed(() => getAvatarColor(this.name()));
  protected readonly sizeClass = computed(() =>
    this.size() === 'lg' ? 'avatar-circle-lg' : this.size() === 'sm' ? 'avatar-circle-sm' : ''
  );
}
