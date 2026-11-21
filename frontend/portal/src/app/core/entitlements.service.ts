import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AgencyFeature, AgencyPlanDto } from './api.types';
import { AuthService } from './auth.service';

/**
 * What the signed-in agency's plan includes. Only decides what the portal shows; the API checks
 * the plan again on every write.
 */
@Injectable({ providedIn: 'root' })
export class EntitlementsService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  private readonly state = signal<AgencyPlanDto | null>(null);
  private pending: { agencyId: string; request: Promise<AgencyPlanDto | null> } | null = null;

  /** Null until loaded, for operators, and after a failed load. */
  readonly plan = computed(() => {
    const plan = this.state();
    return plan && plan.agencyId === this.auth.user()?.agencyId ? plan : null;
  });

  has(feature: AgencyFeature): boolean {
    const plan = this.plan();
    return !!plan && plan.isActive && plan.features.includes(feature);
  }

  /** Loads once per signed-in agency; a failure reads as "nothing extra included". */
  load(): Promise<AgencyPlanDto | null> {
    const agencyId = this.auth.user()?.agencyId;
    if (!agencyId) return Promise.resolve(null);
    if (this.pending?.agencyId !== agencyId) {
      const request = firstValueFrom(this.http.get<AgencyPlanDto>('/api/v1/agency/plan'))
        .then(plan => {
          this.state.set(plan);
          return plan;
        })
        .catch(() => {
          this.pending = null;
          return null;
        });
      this.pending = { agencyId, request };
    }
    return this.pending.request;
  }
}

/** Keeps a plan-gated area closed: without the feature the agency lands on its start page. */
export function featureGuard(feature: AgencyFeature): CanActivateFn {
  return async () => {
    const entitlements = inject(EntitlementsService);
    const router = inject(Router);
    await entitlements.load();
    return entitlements.has(feature) ? true : router.createUrlTree(['/agency']);
  };
}
