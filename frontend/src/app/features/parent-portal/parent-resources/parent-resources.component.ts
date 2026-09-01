import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CardModule } from 'primeng/card';
import { SelectModule } from 'primeng/select';
import { EmptyStateComponent } from '../../../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../../../shared/ui/page-header.component';
import { ResourceListComponent } from '../../resources/resource-list.component';
import { PortalResource } from '../../resources/resources.models';
import { ResourcesService } from '../../resources/resources.service';
import { MyChild } from '../parent-portal.models';
import { ParentPortalService } from '../parent-portal.service';

/**
 * חומרי הלימוד של ילדי ההורה — צפייה בלבד. כשיש יותר מילד אחד מוצג סינון,
 * והוא מופעל בצד הלקוח: כל החומרים כבר נטענו בקריאה אחת.
 */
@Component({
  selector: 'app-parent-resources',
  imports: [FormsModule, CardModule, SelectModule, EmptyStateComponent, PageHeaderComponent, ResourceListComponent],
  templateUrl: './parent-resources.component.html'
})
export class ParentResourcesComponent implements OnInit {
  private readonly resourcesService = inject(ResourcesService);
  private readonly parentService = inject(ParentPortalService);

  protected readonly allResources = signal<PortalResource[]>([]);
  protected readonly children = signal<MyChild[]>([]);
  protected readonly selectedChildId = signal<string | null>(null);
  protected readonly loading = signal(true);

  protected readonly resources = computed(() => {
    const childId = this.selectedChildId();
    return childId ? this.allResources().filter(r => r.studentId === childId) : this.allResources();
  });

  ngOnInit(): void {
    this.resourcesService.myChildrensResources().subscribe({
      next: resources => {
        this.allResources.set(resources);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
    this.parentService.myChildren().subscribe(children => this.children.set(children));
  }
}
