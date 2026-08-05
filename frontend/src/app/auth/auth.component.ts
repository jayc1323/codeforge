import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../services/auth.service';

@Component({
  selector: 'app-auth',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './auth.component.html',
  styleUrl: './auth.component.css'
})
export class AuthComponent {
  private auth = inject(AuthService);
  private router = inject(Router);

  mode: 'login' | 'register' = 'login';
  email = '';
  password = '';
  errorMessage = '';
  busy = false;

  submit(): void {
    if (this.busy) return;
    this.errorMessage = '';

    if (!this.email || !this.password) {
      this.errorMessage = 'Email and password are required.';
      return;
    }
    if (this.mode === 'register' && this.password.length < 8) {
      this.errorMessage = 'Password must be at least 8 characters.';
      return;
    }

    this.busy = true;
    const request$ = this.mode === 'login'
      ? this.auth.login(this.email, this.password)
      : this.auth.register(this.email, this.password);

    request$.subscribe({
      next: () => this.router.navigate(['/app']),
      error: (err) => {
        this.busy = false;
        const body = err?.error;
        this.errorMessage =
          body?.error ??
          (Array.isArray(body?.errors) ? body.errors.join(' ') : null) ??
          'Could not reach the CodeForge API.';
      }
    });
  }

  switchMode(): void {
    this.mode = this.mode === 'login' ? 'register' : 'login';
    this.errorMessage = '';
  }
}
