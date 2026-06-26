import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, map, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthResponse, LoginRequest, MessageResponse, RegisterRequest, Role } from './auth.models';

interface Session {
  accessToken: string;
  refreshToken: string;
  email: string;
  roles: Role[];
}

const STORAGE_KEY = 'talmidon_session';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/auth`;

  private readonly session = signal<Session | null>(this.load());

  readonly currentEmail = computed(() => this.session()?.email ?? null);
  readonly roles = computed<Role[]>(() => this.session()?.roles ?? []);
  readonly isAuthenticated = computed(() => !!this.session()?.accessToken);

  accessToken(): string | null {
    return this.session()?.accessToken ?? null;
  }

  refreshTokenValue(): string | null {
    return this.session()?.refreshToken ?? null;
  }

  hasRole(role: Role): boolean {
    return this.roles().includes(role);
  }

  register(request: RegisterRequest): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${this.api}/register`, request);
  }

  resendConfirmation(email: string): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${this.api}/resend-confirmation`, { email });
  }

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.api}/login`, request).pipe(
      tap(response => this.store(response))
    );
  }

  /** מרענן את אסימון הגישה ומחזיר את החדש. משמש את ה-interceptor. */
  refresh(): Observable<string> {
    return this.http.post<AuthResponse>(`${this.api}/refresh`, {
      refreshToken: this.refreshTokenValue()
    }).pipe(
      tap(response => this.store(response)),
      map(response => response.accessToken)
    );
  }

  logout(): void {
    const refreshToken = this.refreshTokenValue();
    if (refreshToken) {
      this.http.post(`${this.api}/logout`, { refreshToken }).subscribe({ error: () => undefined });
    }
    this.clearSession();
  }

  clearSession(): void {
    this.session.set(null);
    localStorage.removeItem(STORAGE_KEY);
  }

  private store(response: AuthResponse): void {
    const session: Session = {
      accessToken: response.accessToken,
      refreshToken: response.refreshToken,
      email: response.email,
      roles: response.roles
    };
    this.session.set(session);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
  }

  private load(): Session | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      return raw ? (JSON.parse(raw) as Session) : null;
    } catch {
      return null;
    }
  }
}
