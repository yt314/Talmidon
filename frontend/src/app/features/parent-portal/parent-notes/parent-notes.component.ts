import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CardModule } from 'primeng/card';
import { SelectModule } from 'primeng/select';
import { MyChild, ParentNote } from '../parent-portal.models';
import { ParentPortalService } from '../parent-portal.service';

@Component({
  selector: 'app-parent-notes',
  imports: [FormsModule, DatePipe, CardModule, SelectModule],
  templateUrl: './parent-notes.component.html'
})
export class ParentNotesComponent implements OnInit {
  private readonly portalService = inject(ParentPortalService);

  protected readonly children = signal<MyChild[]>([]);
  protected readonly selectedChildId = signal<string | null>(null);
  protected readonly notes = signal<ParentNote[]>([]);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    this.portalService.myChildren().subscribe(children => this.children.set(children));
    this.load();
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
