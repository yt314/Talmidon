import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { MessageModule } from 'primeng/message';
import { PasswordModule } from 'primeng/password';
import { PageHeaderComponent } from '../../../shared/ui/page-header.component';
import { AuthService } from '../../../core/auth/auth.service';
import { extractErrorMessage } from '../../../core/http/extract-error-message';
import { fieldError, isInvalid } from '../../../core/forms/validation-messages';
import { passwordPolicyValidator, passwordsMatchValidator } from '../../../core/forms/validators';

@Component({
  selector: 'app-account-settings',
  imports: [ReactiveFormsModule, ButtonModule, CardModule, MessageModule, PasswordModule, PageHeaderComponent],
  templateUrl: './account-settings.component.html'
})
export class AccountSettingsComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly fieldError = fieldError;
  protected readonly isInvalid = isInvalid;

  protected readonly email = this.auth.currentEmail;

  protected readonly form = this.fb.nonNullable.group(
    {
      currentPassword: ['', [Validators.required]],
      newPassword: ['', [Validators.required, Validators.maxLength(100), passwordPolicyValidator]],
      confirmNewPassword: ['', [Validators.required]]
    },
    { validators: passwordsMatchValidator('newPassword', 'confirmNewPassword') }
  );

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.error.set(null);
    this.saving.set(true);
    const { currentPassword, newPassword } = this.form.getRawValue();
    this.auth.changePassword({ currentPassword, newPassword }).subscribe({
      next: () => {
        this.auth.clearSession();
        this.router.navigate(['/login'], { queryParams: { passwordChanged: 1 } });
      },
      error: err => {
        this.saving.set(false);
        this.error.set(extractErrorMessage(err, 'שינוי הסיסמה נכשל.'));
      }
    });
  }
}
