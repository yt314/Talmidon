import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { SelectButtonModule } from 'primeng/selectbutton';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { extractErrorMessage } from '../../core/http/extract-error-message';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { buildWhatsappLink, hasWhatsapp } from '../../shared/whatsapp/whatsapp.util';
import {
  CONTACT_STATUS_LABELS,
  CONTACT_STATUS_SEVERITY,
  ContactRequest,
  ContactRequestStatus
} from './contact-requests.models';
import { ContactRequestsService } from './contact-requests.service';

/** תיבת הפניות שהגיעו מהספרייה הציבורית. */
@Component({
  selector: 'app-contact-requests',
  imports: [
    FormsModule,
    DatePipe,
    ButtonModule,
    CardModule,
    SelectButtonModule,
    TagModule,
    TooltipModule,
    EmptyStateComponent,
    PageHeaderComponent
  ],
  templateUrl: './contact-requests.component.html'
})
export class ContactRequestsComponent implements OnInit {
  private readonly service = inject(ContactRequestsService);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly statusLabel = (s: ContactRequestStatus): string => CONTACT_STATUS_LABELS[s];
  protected readonly statusSeverity = (s: ContactRequestStatus) => CONTACT_STATUS_SEVERITY[s];
  protected readonly hasWhatsapp = hasWhatsapp;
  protected readonly Status = ContactRequestStatus;

  protected readonly loading = signal(true);
  protected readonly all = signal<ContactRequest[]>([]);
  /** null = הכול. */
  protected readonly filter = signal<ContactRequestStatus | null>(ContactRequestStatus.New);

  protected readonly filterOptions = [
    { label: 'חדשות', value: ContactRequestStatus.New },
    { label: 'בטיפול', value: ContactRequestStatus.Handled },
    { label: 'נסגרו', value: ContactRequestStatus.Closed },
    { label: 'הכול', value: null }
  ];

  protected readonly visible = computed(() => {
    const status = this.filter();
    return status === null ? this.all() : this.all().filter(c => c.status === status);
  });

  ngOnInit(): void {
    this.load();
  }

  protected openWhatsapp(contact: ContactRequest): void {
    const text = `שלום ${contact.fullName}, קיבלתי את פנייתך דרך תלמידון.`;
    const link = buildWhatsappLink(contact.phone, text);
    if (link) window.open(link, '_blank', 'noopener');
  }

  protected setStatus(contact: ContactRequest, status: ContactRequestStatus): void {
    this.service.updateStatus(contact.id, status).subscribe({
      next: () => this.all.set(this.all().map(c => (c.id === contact.id ? { ...c, status } : c))),
      error: err =>
        this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'העדכון נכשל.') })
    });
  }

  protected confirmDelete(contact: ContactRequest): void {
    this.confirmationService.confirm({
      message: `למחוק את הפנייה מ${contact.fullName}? הפעולה אינה הפיכה.`,
      header: 'אישור מחיקה',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'מחק',
      rejectLabel: 'ביטול',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () =>
        this.service.delete(contact.id).subscribe({
          next: () => this.all.set(this.all().filter(c => c.id !== contact.id)),
          error: err =>
            this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'המחיקה נכשלה.') })
        })
    });
  }

  private load(): void {
    this.loading.set(true);
    this.service.list().subscribe({
      next: items => {
        this.all.set(items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
