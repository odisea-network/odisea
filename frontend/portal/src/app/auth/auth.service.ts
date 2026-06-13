import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, switchMap, tap } from 'rxjs';
import { environment } from '../../environments/environment';

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  displayName: string;
  // Self-serve signup: name a new tenant and pick a role ("agency" | "operator" | "both").
  tenantName?: string;
  tenantRole?: string;
}

export interface TokenResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
}

export interface CurrentUser {
  id: string;
  email: string;
  displayName: string;
  role: string;
  tenantType: string | null;
  tenantId: string | null;
}

const TOKEN_KEY = 'od_access_token';
const USER_KEY = 'od_current_user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private base = environment.apiBase;

  // Token + user survive a page refresh via localStorage; signals drive the UI.
  private readonly _token = signal<string | null>(localStorage.getItem(TOKEN_KEY));
  private readonly _user = signal<CurrentUser | null>(readUser());

  readonly user = this._user.asReadonly();
  readonly isAuthenticated = computed(() => this._token() !== null);
  readonly role = computed(() => this._user()?.role ?? null);
  readonly isOperator = computed(() => this._user()?.tenantType === 'Operator');
  readonly isAgency = computed(() => this._user()?.tenantType === 'Agency');

  token(): string | null {
    return this._token();
  }

  /** Logs in, then resolves the current user (the interceptor attaches the fresh token to /auth/me). */
  login(req: LoginRequest): Observable<CurrentUser> {
    return this.http.post<TokenResponse>(`${this.base}/auth/login`, req).pipe(
      tap((res) => this.setToken(res.accessToken)),
      switchMap(() => this.fetchMe()),
    );
  }

  /** Self-serve registration, then resolves the current user (same flow as login). */
  register(req: RegisterRequest): Observable<CurrentUser> {
    return this.http.post<TokenResponse>(`${this.base}/auth/register`, req).pipe(
      tap((res) => this.setToken(res.accessToken)),
      switchMap(() => this.fetchMe()),
    );
  }

  /** Fetches the current user with the active token and caches it. */
  fetchMe(): Observable<CurrentUser> {
    return this.http
      .get<CurrentUser>(`${this.base}/auth/me`)
      .pipe(tap((user) => this.setUser(user)));
  }

  logout(): void {
    this._token.set(null);
    this._user.set(null);
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
  }

  private setToken(token: string): void {
    this._token.set(token);
    localStorage.setItem(TOKEN_KEY, token);
  }

  private setUser(user: CurrentUser): void {
    this._user.set(user);
    localStorage.setItem(USER_KEY, JSON.stringify(user));
  }
}

function readUser(): CurrentUser | null {
  const raw = localStorage.getItem(USER_KEY);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as CurrentUser;
  } catch {
    return null;
  }
}
