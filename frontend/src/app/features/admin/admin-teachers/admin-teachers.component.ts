import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { extractErrorMessage } from '../../../core/http/extract-error-message';
import { getAvatarColor, getInitials } from '../../../shared/avatar/avatar.util';
import { AdminService } from '../admin.service';
import { AdminTeacher } from '../admin.models';

@Component({
  selector: 'app-admin-teachers',
  imports: [DatePipe, ButtonModule, TableModule, TagModule],
  templateUrl: './admin-teachers.component.html'
})
export class AdminTeachersComponent implements OnInit {
  private readonly adminService = inject(AdminService);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly teachers = signal<AdminTeacher[]>([]);
  protected readonly loading = signal(true);
  protected readonly busyTeacherId = signal<string | null>(null);
  protected readonly initials = getInitials;
  protected readonly avatarColor = getAvatarColor;

  ngOnInit(): void {
    this.load();
  }

  confirmLock(teacher: AdminTeacher): void {
    this.confirmationService.confirm({
      header: 'נעילת חשבון מורה',
      message: `לנעול את חשבון ההתחברות של ${teacher.fullName}? המורה לא תוכל להתחבר עד שהנעילה תוסר.`,
      icon: 'pi pi-lock',
      acceptLabel: 'נעל',
      rejectLabel: 'ביטול',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.setLock(teacher, true)
    });
  }

  confirmUnlock(teacher: AdminTeacher): void {
    this.confirmationService.confirm({
      header: 'שחרור נעילה',
      message: `לשחרר את הנעילה מחשבון ${teacher.fullName}?`,
      icon: 'pi pi-lock-open',
      acceptLabel: 'שחרר',
      rejectLabel: 'ביטול',
      accept: () => this.setLock(teacher, false)
    });
  }

  private setLock(teacher: AdminTeacher, lock: boolean): void {
    this.busyTeacherId.set(teacher.id);
    const request$ = lock ? this.adminService.lockTeacher(teacher.id) : this.adminService.unlockTeacher(teacher.id);
    request$.subscribe({
      next: () => {
        this.busyTeacherId.set(null);
        this.messageService.add({ severity: 'success', summary: lock ? 'החשבון ננעל' : 'הנעילה שוחררה' });
        this.load();
      },
      error: err => {
        this.busyTeacherId.set(null);
        this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'הפעולה נכשלה.') });
      }
    });
  }

  private load(): void {
    this.loading.set(true);
    this.adminService.listTeachers().subscribe({
      next: teachers => {
        this.teachers.set(teachers);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
