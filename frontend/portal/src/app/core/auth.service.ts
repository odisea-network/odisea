import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  userType: 'operator' | 'agency';
  role: string;
  agencyId: string | null;
  fullName: string;
}

export interface SessionUser {
  userType: 'operator' | 'agency';
  role: string;
  agencyId: string | null;
  fullName: string;
  email: string;
  expiresAtEpoch: number;
}

/**
 * The access token lives ONLY in memory — nothing readable by scripts is
 * persisted. The session itself is carried by the HttpOnly refresh cookie
 * (set/rotated server-side): a reload silently restores via /auth/refresh,
 * and the interceptor retries one 401 after a single-flight refresh.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  private readonly tokenSignal = signal<string | null>(null);
  private readonly userSignal = signal<SessionUser | null>(null);
  private refreshInFlight: Promise<string | null> | null = null;

  readonly user = this.userSignal.asReadonly();
  readonly isAuthenticated = computed(() => {
    const user = this.userSignal();
    return user !== null && user.expiresAtEpoch * 1000 > Date.now();
  });

  get token(): string | null {
    return this.tokenSignal();
  }

  async login(email: string, password: string): Promise<SessionUser> {
    const response = await firstValueFrom(
      this.http.post<LoginResponse>('/api/v1/auth/login', { email, password }),
    );
    return this.applySession(response);
  }

  /** Called at app start: restores the session from the refresh cookie, if any. */
  async tryRestore(): Promise<void> {
    await this.refreshOnce();
  }

  /** Single-flight: concurrent 401s share one refresh (rotation makes a
   *  second parallel call self-destructive — the family would be revoked). */
  refreshOnce(): Promise<string | null> {
    this.refreshInFlight ??= this.doRefresh().finally(() => {
      this.refreshInFlight = null;
    });
    return this.refreshInFlight;
  }

  logout(): void {
    // Server-side family revocation; local state clears regardless.
    this.http.post('/api/v1/auth/logout', {}).subscribe({ error: () => undefined });
    this.tokenSignal.set(null);
    this.userSignal.set(null);
  }

  private async doRefresh(): Promise<string | null> {
    try {
      const response = await firstValueFrom(
        this.http.post<LoginResponse>('/api/v1/auth/refresh', {}),
      );
      this.applySession(response);
      return response.accessToken;
    } catch {
      this.tokenSignal.set(null);
      this.userSignal.set(null);
      return null;
    }
  }

  private applySession(response: LoginResponse): SessionUser {
    const claims = decodeJwtPayload(response.accessToken);
    const user: SessionUser = {
      userType: response.userType,
      role: response.role,
      agencyId: response.agencyId,
      fullName: response.fullName,
      email: (claims['email'] as string) ?? '',
      expiresAtEpoch: (claims['exp'] as number) ?? 0,
    };
    this.tokenSignal.set(response.accessToken);
    this.userSignal.set(user);
    return user;
  }
}

export function decodeJwtPayload(token: string): Record<string, unknown> {
  try {
    const payload = token.split('.')[1];
    const normalized = payload.replace(/-/g, '+').replace(/_/g, '/');
    return JSON.parse(atob(normalized)) as Record<string, unknown>;
  } catch {
    return {};
  }
}
