import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { MenubarModule } from 'primeng/menubar';
import { ToastModule } from 'primeng/toast';
import { AuthService } from '../../../core/auth/auth.service';
import { Gender } from '../../../core/models/gender';
import { ParentsService } from '../../parents/parents.service';
import { ParentPortalService } from '../parent-portal.service';

@Component({
  selector: 'app-parent-shell',
  imports: [RouterOutlet, MenubarModule, ButtonModule, ToastModule],
  templateUrl: './parent-shell.component.html'
})
export class ParentShellComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly parentsService = inject(ParentsService);
  private readonly portalService = inject(ParentPortalService);

  private readonly gender = signal<Gender | null>(null);
  private readonly childrenNames = signal<string[]>([]);

  protected readonly greeting = computed(() => {
    const relation = this.gender() === Gender.Male ? 'אבא' : this.gender() === Gender.Female ? 'אמא' : 'ההורה';
    const names = this.childrenNames();
    return names.length > 0 ? `שלום, ${relation} של ${names.join(', ')}` : `שלום, ${relation}`;
  });

  protected readonly menuItems: MenuItem[] = [
    { label: 'יומן', icon: 'pi pi-calendar', routerLink: '/parent/lessons' },
    { label: 'הערות', icon: 'pi pi-book', routerLink: '/parent/notes' },
    { label: 'תשלומים', icon: 'pi pi-wallet', routerLink: '/parent/payments' }
  ];

  ngOnInit(): void {
    this.parentsService.myProfile().subscribe(profile => this.gender.set(profile.gender));
    this.portalService.myChildren().subscribe(children => this.childrenNames.set(children.map(c => c.fullName)));
  }

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
