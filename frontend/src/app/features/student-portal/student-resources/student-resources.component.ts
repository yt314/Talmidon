import { Component, OnInit, inject, signal } from '@angular/core';
import { CardModule } from 'primeng/card';
import { EmptyStateComponent } from '../../../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../../../shared/ui/page-header.component';
import { ResourceListComponent } from '../../resources/resource-list.component';
import { PortalResource } from '../../resources/resources.models';
import { ResourcesService } from '../../resources/resources.service';

/** חומרי הלימוד שהמורה שיתפה עם התלמיד — צפייה בלבד. */
@Component({
  selector: 'app-student-resources',
  imports: [CardModule, EmptyStateComponent, PageHeaderComponent, ResourceListComponent],
  templateUrl: './student-resources.component.html'
})
export class StudentResourcesComponent implements OnInit {
  private readonly resourcesService = inject(ResourcesService);

  protected readonly resources = signal<PortalResource[]>([]);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    this.resourcesService.myResources().subscribe({
      next: resources => {
        this.resources.set(resources);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
