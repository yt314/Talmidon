import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { PublicTeacherSummary } from '../public.models';
import { PublicService } from '../public.service';

@Component({
  selector: 'app-teacher-library',
  imports: [FormsModule, RouterLink, ButtonModule, CardModule, InputTextModule, SelectModule, TagModule],
  templateUrl: './teacher-library.component.html'
})
export class TeacherLibraryComponent implements OnInit {
  private readonly publicService = inject(PublicService);

  protected readonly loading = signal(true);
  protected readonly allTeachers = signal<PublicTeacherSummary[]>([]);
  protected readonly subjects = signal<string[]>([]);
  protected readonly search = signal('');
  protected readonly selectedSubject = signal<string | null>(null);

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
}
