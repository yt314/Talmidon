import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { PasswordModule } from 'primeng/password';
import { AuthLayoutComponent } from '../../../shared/ui/auth-layout.component';
import { AuthService } from '../../../core/auth/auth.service';
import { extractErrorMessage } from '../../../core/http/extract-error-message';
import { fieldError, isInvalid } from '../../../core/forms/validation-messages';

/** תואם להודעה המדויקת שמחזיר AuthController.Login כשהחשבון קיים אך המייל טרם אומת. */
const EMAIL_NOT_CONFIRMED_MESSAGE = 'יש לאמת את כתובת המייל לפני התחברות.';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink, InputTextModule, PasswordModule, ButtonModule, MessageModule, AuthLayoutComponent],
  templateUrl: './login.component.html'
})
export class LoginComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly confirmed = signal(false);
  protected readonly passwordChanged = signal(false);
  protected readonly unconfirmedEmail = signal<string | null>(null);
  protected readonly resending = signal(false);
  protected readonly resendDone = signal(false);
  protected readonly fieldError = fieldError;
  protected readonly isInvalid = isInvalid;

  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]]
  });

  ngOnInit(): void {
    if (this.route.snapshot.queryParamMap.get('confirmed') === '1') {
      this.confirmed.set(true);
    }
    if (this.route.snapshot.queryParamMap.get('passwordChanged') === '1') {
      this.passwordChanged.set(true);
    }
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.loading.set(true);
    this.error.set(null);
    this.unconfirmedEmail.set(null);
    this.resendDone.set(false);
    const { email, password } = this.form.getRawValue();
    this.auth.login({ email, password }).subscribe({
      next: () => this.router.navigateByUrl(this.auth.homePath()),
      error: err => {
        const message = extractErrorMessage(err, 'ההתחברות נכשלה. נסה שוב.');
        this.error.set(message);
        if (message === EMAIL_NOT_CONFIRMED_MESSAGE) {
          this.unconfirmedEmail.set(email);
        }
        this.loading.set(false);
      }
    });
  }

  resendConfirmation(): void {
    const email = this.unconfirmedEmail();
    if (!email) return;
    this.resending.set(true);
    this.auth.resendConfirmation(email).subscribe({
      next: () => {
        this.resending.set(false);
        this.resendDone.set(true);
      },
      error: () => {
        // תגובה גנרית תמיד מוצגת גם בכשל, כדי לא לחשוף אם המייל קיים במערכת
        this.resending.set(false);
        this.resendDone.set(true);
      }
    });
  }
}
