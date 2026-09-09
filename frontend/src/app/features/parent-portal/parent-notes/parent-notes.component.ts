
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { SelectModule } from 'primeng/select';
import { SkeletonModule } from 'primeng/skeleton';
import { EmptyStateComponent } from '../../../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../../../shared/ui/page-header.component';
import { getAvatarColor, getInitials } from '../../../shared/avatar/avatar.util';
import { MyChild, ParentNote } from '../parent-portal.models';
import { ParentPortalService } from '../parent-portal.service';
import { IsraelDatePipe } from '../../../core/i18n/israel-date.pipe';

@Component({
  selector: 'app-parent-notes',
  imports: [ FormsModule, ButtonModule, CardModule, SelectModule, SkeletonModule, PageHeaderComponent, EmptyStateComponent, IsraelDatePipe],
  templateUrl: './parent-notes.component.html'
})
export class ParentNotesComponent implements OnInit {
  private readonly portalService = inject(ParentPortalService);
  private readonly router = inject(Router);

  protected readonly children = signal<MyChild[]>([]);
  protected readonly selectedChildId = signal<string | null>(null);
  protected readonly notes = signal<ParentNote[]>([]);
  protected readonly loading = signal(true);
  protected readonly initials = getInitials;
  protected readonly avatarColor = getAvatarColor;

  ngOnInit(): void {
    this.portalService.myChildren().subscribe(children => this.children.set(children));
    this.load();
  }

  /** תגובה על הערה — נפתחת כשיחה במסך ההודעות, עם ההערה ועם הילד שאליו היא שייכת. */
  protected reply(note: ParentNote): void {
    this.router.navigate(['/parent/messages'], {
      queryParams: { noteId: note.id, studentId: note.studentId, subject: 'תגובה להערה' }
    });
  }

  onChildChange(): void {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.portalService.myNotes(this.selectedChildId()).subscribe({
      next: notes => {
        this.notes.set(notes);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
