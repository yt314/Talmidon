import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { PortalResource, StudentResource, resourceColor, resourceHost, resourceIcon, resourceKindLabel } from './resources.models';

/**
 * רשימת חומרי לימוד — משותפת למסך המורה ולשני הפורטלים, כדי שחומר ייראה אותו דבר
 * בכל מקום. ההבדלים בין המסכים מועברים כקלט: שם התלמיד (רלוונטי רק להורה עם כמה
 * ילדים) וכפתור המחיקה (רק למורה).
 */
@Component({
  selector: 'app-resource-list',
  imports: [DatePipe, ButtonModule, TooltipModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ul class="resource-list">
      @for (resource of resources(); track resource.id) {
        <li class="resource-item" [style.--resource-tone]="color(resource.url)">
          <span class="resource-icon"><i class="pi {{ icon(resource.url) }}"></i></span>

          <div class="resource-body">
            <!-- noopener/noreferrer: הקישור חיצוני ומגיע מתוכן שהמורה הזינה -->
            <a class="resource-title" [href]="resource.url" target="_blank" rel="noopener noreferrer">{{ resource.title }}</a>
            <span class="resource-meta">
              {{ kindLabel(resource.url) }} · {{ host(resource.url) }}
              @if (showStudentName()) {
                · {{ studentNameOf(resource) }}
              }
              · {{ resource.createdAt | date: 'dd/MM/yyyy' }}
            </span>
            @if (resource.description) {
              <p class="resource-description">{{ resource.description }}</p>
            }
          </div>

          @if (deletable()) {
            <p-button
              icon="pi pi-trash"
              severity="danger"
              [text]="true"
              [rounded]="true"
              pTooltip="מחיקה"
              tooltipPosition="top"
              [ariaLabel]="'מחיקת ' + resource.title"
              (onClick)="remove.emit(resource)" />
          }
        </li>
      }
    </ul>
  `
})
export class ResourceListComponent {
  readonly resources = input.required<readonly StudentResource[]>();
  /** מוצג בפורטל ההורה, שבו מעורבבים חומרים של כמה ילדים. */
  readonly showStudentName = input(false);
  readonly deletable = input(false);
  readonly remove = output<StudentResource>();

  protected readonly icon = resourceIcon;
  protected readonly color = resourceColor;
  protected readonly host = resourceHost;
  protected readonly kindLabel = resourceKindLabel;

  protected studentNameOf(resource: StudentResource): string {
    return (resource as PortalResource).studentName ?? '';
  }
}
