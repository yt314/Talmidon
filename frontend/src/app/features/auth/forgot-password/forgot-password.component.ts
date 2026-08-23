import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { AuthService } from '../../../core/auth/auth.service';
import { fieldError, isInvalid } from '../../../core/forms/validation-messages';

@Component({
  selector: 'app-forgot-password',
  imports: [ReactiveFormsModule, RouterLink, ButtonModule, CardModule, InputTextModule],
  templateUrl: './forgot-password.component.html'
})
export class ForgotPasswordComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);

  protected readonly loading = signal(false);
  protected readonly done = signal(false);
  protected readonly successMessage = signal('');
  protected readonly fieldError = fieldError;
  protected readonly isInvalid = isInvalid;

  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]]
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.loading.set(true);
    this.auth.forgotPassword(this.form.getRawValue().email).subscribe({
      next: res => {
        this.successMessage.set(res.message);
        this.done.set(true);
        this.loading.set(false);
      },
      error: () => {
        // תגובה גנרית מוצגת תמיד, גם בכשל, כדי לא לחשוף אם המייל קיים במערכת
        this.successMessage.set('אם הכתובת רשומה במערכת, נשלח אליה קישור לאיפוס סיסמה. נא לבדוק את תיבת הדואר.');
        this.done.set(true);
        this.loading.set(false);
      }
    });
  }
}
