import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { CardModule } from 'primeng/card';
import { StudentNote } from '../student-portal.models';
import { StudentPortalService } from '../student-portal.service';

@Component({
  selector: 'app-student-notes',
  imports: [DatePipe, CardModule],
  templateUrl: './student-notes.component.html'
})
export class StudentNotesComponent implements OnInit {
  private readonly portalService = inject(StudentPortalService);

  protected readonly notes = signal<StudentNote[]>([]);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    this.portalService.myNotes().subscribe({
      next: notes => {
        this.notes.set(notes);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
