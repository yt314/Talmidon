import { DatePipe } from '@angular/common';
import { Component, DestroyRef, OnInit, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { Popover, PopoverModule } from 'primeng/popover';
import { EmptyStateComponent } from '../../../shared/ui/empty-state.component';
import { timer } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { AppNotification, NotificationType, notificationIcon } from '../notifications.models';
import { NotificationsService } from '../notifications.service';

@Component({
  selector: 'app-notifications-bell',
  imports: [DatePipe, ButtonModule, PopoverModule, EmptyStateComponent],
  templateUrl: './notifications-bell.component.html'
})
export class NotificationsBellComponent implements OnInit {
  private readonly notificationsService = inject(NotificationsService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  private readonly panel = viewChild.required<Popover>('panel');

  protected readonly notifications = signal<AppNotification[]>([]);
  protected readonly unreadCount = signal(0);
  protected readonly loading = signal(false);

  protected readonly icon = (type: NotificationType): string => notificationIcon(type);

  ngOnInit(): void {
    // רענון מונה ההתראות שלא נקראו כל 60 שניות
    timer(0, 60_000)
      .pipe(
        switchMap(() => this.notificationsService.unreadCount()),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({ next: res => this.unreadCount.set(res.count), error: () => {} });
  }

  toggle(event: Event): void {
    this.loadList();
    this.panel().toggle(event);
  }

  open(notification: AppNotification): void {
    this.notificationsService.markRead(notification.id).subscribe(() => {
      this.notifications.update(list => list.map(n => (n.id === notification.id ? { ...n, isRead: true } : n)));
      this.refreshCount();
    });
    this.panel().hide();
    if (notification.linkPath) this.router.navigateByUrl(notification.linkPath);
  }

  markAllRead(): void {
    this.notificationsService.markAllRead().subscribe(() => {
      this.notifications.update(list => list.map(n => ({ ...n, isRead: true })));
      this.unreadCount.set(0);
    });
  }

  private loadList(): void {
    this.loading.set(true);
    this.notificationsService.list().subscribe({
      next: items => {
        this.notifications.set(items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  private refreshCount(): void {
    this.notificationsService.unreadCount().subscribe({ next: res => this.unreadCount.set(res.count), error: () => {} });
  }
}
