import { Component, computed, inject, input, output } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService, SessionUser } from '../core/auth.service';
import { BrandingService } from '../core/branding.service';
import { ButtonDirective, Icon, IconName } from '../shared/ui';

export interface NavItem {
  label: string;
  link: string;
  icon: IconName;
  /** Badge count shown at the right edge (pending items). */
  count?: number | null;
  exact?: boolean;
}

export interface NavGroup {
  label?: string;
  items: NavItem[];
}

export interface NavAction {
  label: string;
  link: string;
  icon: IconName;
}

const ROLE_LABELS: Record<string, string> = {
  'agency:AgencyAdmin': 'Agency admin',
  'agency:Agent': 'Agent',
  'operator:Admin': 'Operator admin',
  'operator:Operator': 'Operator',
};

export function roleLabel(user: SessionUser | null): string {
  if (!user) return '';
  return ROLE_LABELS[`${user.userType}:${user.role}`] ?? user.role;
}

/** The branded sidebar: operator mark, one primary action, grouped links, the signed-in user. */
@Component({
  selector: 'app-side-nav',
  imports: [RouterLink, RouterLinkActive, ButtonDirective, Icon],
  templateUrl: './side-nav.html',
  styleUrl: './side-nav.scss',
  host: { role: 'navigation', '[attr.aria-label]': 'area()' },
})
export class SideNav {
  protected readonly brand = inject(BrandingService);
  protected readonly auth = inject(AuthService);

  readonly area = input.required<string>();
  readonly groups = input.required<NavGroup[]>();
  readonly primaryAction = input<NavAction | null>(null);
  readonly signOut = output<void>();

  protected readonly role = computed(() => roleLabel(this.auth.user()));
  protected readonly initials = computed(() => {
    const name = this.auth.user()?.fullName ?? '';
    return name.split(/\s+/).filter(Boolean).slice(0, 2).map(part => part[0].toUpperCase()).join('');
  });
}
