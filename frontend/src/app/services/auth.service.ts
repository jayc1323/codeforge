import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap } from 'rxjs';

export interface AuthResponse {
  token: string;
  email: string;
  expiresAt: string;
}

const TOKEN_KEY = 'codeforge_token';
const EMAIL_KEY = 'codeforge_email';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);

  private readonly userSubject = new BehaviorSubject<string | null>(this.readStoredEmail());
  /** Emits the logged-in user's email, or null when logged out. */
  readonly user$: Observable<string | null> = this.userSubject.asObservable();

  get isLoggedIn(): boolean {
    return this.getToken() !== null;
  }

  get currentEmail(): string | null {
    return this.userSubject.value;
  }

  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  register(email: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>('/api/auth/register', { email, password })
      .pipe(tap((response) => this.store(response)));
  }

  login(email: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>('/api/auth/login', { email, password })
      .pipe(tap((response) => this.store(response)));
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(EMAIL_KEY);
    this.userSubject.next(null);
  }

  private store(response: AuthResponse): void {
    localStorage.setItem(TOKEN_KEY, response.token);
    localStorage.setItem(EMAIL_KEY, response.email);
    this.userSubject.next(response.email);
  }

  private readStoredEmail(): string | null {
    return this.getToken() ? localStorage.getItem(EMAIL_KEY) : null;
  }
}
