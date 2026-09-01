import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { DividerModule } from 'primeng/divider';
import { SkeletonModule } from 'primeng/skeleton';
import { TagModule } from 'primeng/tag';
import { getAvatarColor, getInitials } from '../../../shared/avatar/avatar.util';
import { EmptyStateComponent } from '../../../shared/ui/empty-state.component';
import { ThemeToggleComponent } from '../../../shared/ui/theme-toggle.component';
import { PublicTeacherDetail } from '../public.models';
import { PublicService } from '../public.service';

@Component({
  selector: 'app-teacher-profile',
  imports: [
    RouterLink,
    ButtonModule,
    CardModule,
    DividerModule,
    SkeletonModule,
    TagModule,
    EmptyStateComponent,
    ThemeToggleComponent
  ],
  templateUrl: './teacher-profile.component.html'
})
export class TeacherProfileComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly publicService = inject(PublicService);

  protected readonly initials = getInitials;
  protected readonly avatarColor = getAvatarColor;

  protected readonly loading = signal(true);
  protected readonly teacher = signal<PublicTeacherDetail | null>(null);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.loading.set(false);
      return;
    }

    this.publicService.getTeacher(id).subscribe({
      next: teacher => {
        this.teacher.set(teacher);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
