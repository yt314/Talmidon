import { Component, OnInit, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { extractErrorMessage } from '../../../core/http/extract-error-message';
import { fieldError, isInvalid } from '../../../core/forms/validation-messages';
import { httpUrlValidator } from '../../../core/forms/validators';
import { EmptyStateComponent } from '../../../shared/ui/empty-state.component';
import { ResourceListComponent } from '../../resources/resource-list.component';
import { StudentResource } from '../../resources/resources.models';
import { ResourcesService } from '../../resources/resources.service';

/** חומרי הלימוד בכרטיס התלמיד — כל מה שנוסף כאן גלוי לתלמיד/ה ולהוריו בפורטל. */
@Component({
  selector: 'app-student-resources-card',
  imports: [ReactiveFormsModule, ButtonModule, CardModule, DialogModule, InputTextModule, TextareaModule, EmptyStateComponent, ResourceListComponent],
  templateUrl: './student-resources-card.component.html',
  host: { class: 'block' }
})
export class StudentResourcesCardComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly resourcesService = inject(ResourcesService);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  readonly studentId = input.required<string>();

  protected readonly resources = signal<StudentResource[]>([]);
  protected readonly loading = signal(true);
  protected readonly showDialog = signal(false);
  protected readonly saving = signal(false);
  protected readonly fieldError = fieldError;
  protected readonly isInvalid = isInvalid;

  protected readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    url: ['', [Validators.required, Validators.maxLength(2000), httpUrlValidator]],
    description: ['', [Validators.maxLength(1000)]]
  });

  ngOnInit(): void {
    this.load();
  }

  protected openAdd(): void {
    this.form.reset({ title: '', url: '', description: '' });
    this.showDialog.set(true);
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw = this.form.getRawValue();
    this.saving.set(true);
    this.resourcesService
      .create(this.studentId(), { title: raw.title, url: raw.url.trim(), description: raw.description || null })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.showDialog.set(false);
          this.messageService.add({ severity: 'success', summary: 'החומר נוסף' });
          this.load();
        },
        error: err => {
          this.saving.set(false);
          this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'הוספת החומר נכשלה.') });
        }
      });
  }

  protected confirmDelete(resource: StudentResource): void {
    this.confirmationService.confirm({
      message: `למחוק את "${resource.title}"? הפעולה אינה הפיכה.`,
      header: 'אישור מחיקה',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'מחק',
      rejectLabel: 'ביטול',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.delete(resource.id)
    });
  }

  private delete(resourceId: string): void {
    this.resourcesService.delete(this.studentId(), resourceId).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'החומר נמחק' });
        this.load();
      },
      error: err =>
        this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'המחיקה נכשלה.') })
    });
  }

  private load(): void {
    this.loading.set(true);
    this.resourcesService.listForStudent(this.studentId()).subscribe({
      next: resources => {
        this.resources.set(resources);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
