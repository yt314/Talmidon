import { Component, DOCUMENT, DestroyRef, OnInit, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { Popover, PopoverModule } from 'primeng/popover';
import { EmptyStateComponent } from '../../../shared/ui/empty-state.component';
import { switchMap } from 'rxjs/operators';
import { pollWhileVisible } from '../../../core/polling/poll-while-visible';
import { AppNotification, NotificationType, notificationIcon } from '../notifications.models';
import { NotificationsService } from '../notifications.service';
import { IsraelDatePipe } from '../../../core/i18n/israel-date.pipe';

@Component({
  selector: 'app-notifications-bell',
  imports: [ButtonModule, PopoverModule, EmptyStateComponent, IsraelDatePipe],
  templateUrl: './notifications-bell.component.html',
})
export class NotificationsBellComponent implements OnInit {
  private readonly notificationsService = inject(NotificationsService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly doc = inject(DOCUMENT);

  private readonly panel = viewChild.required<Popover>('panel');

  protected readonly notifications = signal<AppNotification[]>([]);
  protected readonly unreadCount = signal(0);
  protected readonly loading = signal(false);

  protected readonly icon = (type: NotificationType): string => notificationIcon(type);

  ngOnInit(): void {
    // Refresh the unread counter once a minute, but only while the tab is
    // actually being looked at.
    pollWhileVisible(this.doc, 60_000)
      .pipe(
        switchMap(() => this.notificationsService.unreadCount()),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({ next: (res) => this.unreadCount.set(res.count), error: () => {} });
  }

  toggle(event: Event): void {
    this.loadList();
    this.panel().toggle(event);
  }

  open(notification: AppNotification): void {
    this.notificationsService.markRead(notification.id).subscribe(() => {
      this.notifications.update((list) =>
        list.map((n) => (n.id === notification.id ? { ...n, isRead: true } : n)),
      );
      this.refreshCount();
    });
    this.panel().hide();
    if (notification.linkPath) this.router.navigateByUrl(notification.linkPath);
  }

  markAllRead(): void {
    this.notificationsService.markAllRead().subscribe(() => {
      this.notifications.update((list) => list.map((n) => ({ ...n, isRead: true })));
      this.unreadCount.set(0);
    });
  }

  private loadList(): void {
    this.loading.set(true);
    this.notificationsService.list().subscribe({
      next: (items) => {
        this.notifications.set(items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  private refreshCount(): void {
    this.notificationsService
      .unreadCount()
      .subscribe({ next: (res) => this.unreadCount.set(res.count), error: () => {} });
  }
}
