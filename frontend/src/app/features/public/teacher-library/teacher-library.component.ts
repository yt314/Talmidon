import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AccordionModule } from 'primeng/accordion';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { SkeletonModule } from 'primeng/skeleton';
import { TagModule } from 'primeng/tag';
import { AvatarComponent } from '../../../shared/avatar/avatar.component';
import { teacherPhotoUrl } from '../../../shared/avatar/photo-url.util';
import { CountUpDirective } from '../../../shared/ui/count-up.directive';
import { EmptyStateComponent } from '../../../shared/ui/empty-state.component';
import { RevealDirective } from '../../../shared/ui/reveal.directive';
import { SpotlightDirective } from '../../../shared/ui/spotlight.directive';
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
    AccordionModule,
    EmptyStateComponent,
    ThemeToggleComponent,
    RevealDirective,
    SpotlightDirective,
    CountUpDirective,
    AvatarComponent
  ],
  templateUrl: './teacher-library.component.html'
})
export class TeacherLibraryComponent implements OnInit {
  private readonly publicService = inject(PublicService);

  protected readonly photoUrl = teacherPhotoUrl;

  /**
   * כותרת אזור התוצאות. כשיש סינון פעיל היא מדווחת כמה נמצאו — הרשת יושבת מיד
   * מתחת לשדה החיפוש, ובלי מספר לא ברור אם המסנן בכלל תפס.
   */
  protected readonly resultsTitle = computed(() => {
    if (!this.search().trim() && !this.selectedSubject()) return 'המורות שלנו';
    const count = this.teachers().length;
    if (count === 0) return 'לא נמצאו מורות';
    return count === 1 ? 'נמצאה מורה אחת' : `נמצאו ${count} מורות`;
  });

  protected readonly year = new Date().getFullYear();

  /** שאלות נפוצות בדף הנחיתה. טקסט קבוע — אין לו מקור בשרת. */
  protected readonly faq = [
    {
      question: 'האם השימוש בספרייה עולה כסף?',
      answer:
        'לא. הצפייה בספרייה והפנייה למורה חינמיות ולא דורשות הרשמה. התשלום על השיעורים עצמם מתבצע ישירות מול המורה, ותלמידון לא גובה עמלה.'
    },
    {
      question: 'איך יוצרים קשר עם מורה?',
      answer:
        'בפרופיל של כל מורה מופיעים פרטי הקשר שהיא בחרה לפרסם — טלפון, וואטסאפ או מייל. הפנייה היא ישירה אליה.'
    },
    {
      question: 'אני מורה. איך מתחילים?',
      answer:
        'נרשמות, מאשרות את כתובת המייל, ומוסיפות תלמיד ראשון. הפרסום בספרייה הציבורית הוא בחירה נפרדת — אפשר להשתמש במערכת לניהול בלבד, בלי להופיע בספרייה.'
    },
    {
      question: 'מה ההורה והתלמיד רואים?',
      answer:
        'ההורה רואה את יומן השיעורים של ילדיו, את ההערות שסימנתן כגלויות להורים, את חומרי הלימוד ואת מצב התשלומים. התלמיד רואה את היומן, את ההערות שסומנו גלויות לו ואת חומרי הלימוד — בלי שום מידע על כסף.'
    },
    {
      question: 'האם המידע שלי מופרד ממורות אחרות?',
      answer:
        'כן, בשלוש שכבות בלתי תלויות: סינון ברמת השאילתה, אכיפה בשמירה למסד, ומפתחות זרים מורכבים ברמת מסד הנתונים. גם אם שכבה אחת תיכשל, מורה אחרת לא תוכל להגיע לנתונים שלכן.'
    },
    {
      question: 'אפשר לבטל או לדחות שיעור דרך המערכת?',
      answer:
        'כן. הורה יכול לשלוח בקשת ביטול או דחייה, המורה מקבלת התראה ומאשרת או דוחה — והיומן מתעדכן בהתאם. מדיניות הביטולים של כל מורה מופיעה בפרופיל שלה.'
    }
  ];

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
