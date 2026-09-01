import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { SkeletonModule } from 'primeng/skeleton';
import { TagModule } from 'primeng/tag';
import { getAvatarColor, getInitials } from '../../../shared/avatar/avatar.util';
import { EmptyStateComponent } from '../../../shared/ui/empty-state.component';
import { ThemeToggleComponent } from '../../../shared/ui/theme-toggle.component';
import { PublicTeacherSummary } from '../public.models';
import { PublicService } from '../public.service';

@Component({
  selector: 'app-teacher-library',
  imports: [
    FormsModule,
    RouterLink,
    ButtonModule,
    CardModule,
    InputTextModule,
    SelectModule,
    SkeletonModule,
    TagModule,
    EmptyStateComponent,
    ThemeToggleComponent
  ],
  templateUrl: './teacher-library.component.html'
})
export class TeacherLibraryComponent implements OnInit {
  private readonly publicService = inject(PublicService);

  protected readonly initials = getInitials;
  protected readonly avatarColor = getAvatarColor;

  protected readonly loading = signal(true);
  protected readonly allTeachers = signal<PublicTeacherSummary[]>([]);
  protected readonly subjects = signal<string[]>([]);
  protected readonly search = signal('');
  protected readonly selectedSubject = signal<string | null>(null);

  /** המחיר הזול ביותר בספרייה — מוצג כ"החל מ־" ברצועת הפתיחה. */
  protected readonly lowestPrice = computed(() => {
    const prices = this.allTeachers().map(t => t.defaultPricePerLesson);
    return prices.length > 0 ? Math.min(...prices) : null;
  });

  protected readonly teachers = computed(() => {
    const search = this.search().trim().toLowerCase();
    const subject = this.selectedSubject();
    return this.allTeachers().filter(
      teacher =>
        (!search || teacher.fullName.toLowerCase().includes(search)) &&
        (!subject || teacher.subjects.includes(subject))
    );
  });

  ngOnInit(): void {
    this.publicService.listTeachers().subscribe({
      next: teachers => {
        this.allTeachers.set(teachers);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
    this.publicService.listSubjects().subscribe(subjects => this.subjects.set(subjects));
  }

  protected clearFilters(): void {
    this.search.set('');
    this.selectedSubject.set(null);
  }

  /**
   * השהיית האנימציה של כרטיס לפי מקומו ברשימה, כדי שהכרטיסים "יעלו" בזה אחר זה.
   * נעצרת אחרי 8 כרטיסים — אחרת רשימה ארוכה נראית איטית במקום חיה.
   */
  protected riseDelay(index: number): string {
    return `${Math.min(index, 8) * 0.05}s`;
  }
}
