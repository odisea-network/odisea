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

const STORAGE_KEY = 'odisea.session';

/**
 * Holds the JWT for the browser session. sessionStorage (not localStorage) so
 * the token dies with the tab; refresh-token rotation is tracked in #100 and
 * will replace the long-lived access token.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  private readonly tokenSignal = signal<string | null>(null);
  private readonly userSignal = signal<SessionUser | null>(null);

  readonly user = this.userSignal.asReadonly();
  readonly isAuthenticated = computed(() => {
    const user = this.userSignal();
    return user !== null && user.expiresAtEpoch * 1000 > Date.now();
  });

  constructor() {
    this.restore();
  }

  get token(): string | null {
    return this.tokenSignal();
  }

  async login(email: string, password: string): Promise<SessionUser> {
    const response = await firstValueFrom(
      this.http.post<LoginResponse>('/api/v1/auth/login', { email, password }),
    );
    const user = this.toUser(response);
    this.tokenSignal.set(response.accessToken);
    this.userSignal.set(user);
    this.persist(response.accessToken, user);
    return user;
  }

  logout(): void {
    this.tokenSignal.set(null);
    this.userSignal.set(null);
    try {
      sessionStorage.removeItem(STORAGE_KEY);
    } catch {
      /* storage unavailable — nothing to clear */
    }
  }

  private toUser(response: LoginResponse): SessionUser {
    const claims = decodeJwtPayload(response.accessToken);
    return {
      userType: response.userType,
      role: response.role,
      agencyId: response.agencyId,
      fullName: response.fullName,
      email: (claims['email'] as string) ?? '',
      expiresAtEpoch: (claims['exp'] as number) ?? 0,
    };
  }

  private persist(token: string, user: SessionUser): void {
    try {
      sessionStorage.setItem(STORAGE_KEY, JSON.stringify({ token, user }));
    } catch {
      /* private mode etc. — session just won't survive a reload */
    }
  }

  private restore(): void {
    try {
      const raw = sessionStorage.getItem(STORAGE_KEY);
      if (!raw) return;
      const { token, user } = JSON.parse(raw) as { token: string; user: SessionUser };
      if (user.expiresAtEpoch * 1000 > Date.now()) {
        this.tokenSignal.set(token);
        this.userSignal.set(user);
      } else {
        sessionStorage.removeItem(STORAGE_KEY);
      }
    } catch {
      /* corrupted or unavailable storage — start signed out */
    }
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
