import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { PasswordModule } from 'primeng/password';
import { AuthLayoutComponent } from '../../../shared/ui/auth-layout.component';
import { AuthService } from '../../../core/auth/auth.service';
import { extractErrorMessage } from '../../../core/http/extract-error-message';
import { fieldError, isInvalid } from '../../../core/forms/validation-messages';
import { passwordPolicyValidator, passwordsMatchValidator } from '../../../core/forms/validators';

@Component({
  selector: 'app-set-password',
  imports: [ReactiveFormsModule, RouterLink, ButtonModule, MessageModule, PasswordModule, AuthLayoutComponent],
  templateUrl: './set-password.component.html'
})
export class SetPasswordComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly done = signal(false);
  protected readonly linkInvalid = signal(false);
  protected readonly fieldError = fieldError;
  protected readonly isInvalid = isInvalid;

  private userId = '';
  private token = '';

  protected readonly form = this.fb.nonNullable.group(
    {
      password: ['', [Validators.required, Validators.maxLength(100), passwordPolicyValidator]],
      confirmPassword: ['', [Validators.required]]
    },
    { validators: passwordsMatchValidator('password', 'confirmPassword') }
  );

  ngOnInit(): void {
    const params = this.route.snapshot.queryParamMap;
    this.userId = params.get('userId') ?? '';
    this.token = params.get('token') ?? '';
    if (!this.userId || !this.token) {
      this.linkInvalid.set(true);
    }
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.loading.set(true);
    this.error.set(null);

    const { password } = this.form.getRawValue();
    this.auth.setPassword({ userId: this.userId, token: this.token, password }).subscribe({
      next: () => {
        this.loading.set(false);
        this.done.set(true);
      },
      error: err => {
        this.loading.set(false);
        this.error.set(extractErrorMessage(err, 'קביעת הסיסמה נכשלה. ייתכן שהקישור פג תוקף.'));
      }
    });
  }

  goToLogin(): void {
    this.router.navigate(['/login']);
  }
}
