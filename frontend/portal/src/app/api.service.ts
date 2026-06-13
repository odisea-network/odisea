import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';

export interface OfferDto {
  id: string;
  title: string;
  description: string;
  country: string;
  city: string;
  price: number;
  currency: string;
  boardBasis: string;
  transport: string;
  durationNights: number;
  imageUrl: string;
  visibility: string;
  ownerType?: string;
  status?: string;
  tags: string[];
}

export interface BulkRowError {
  index: number;
  error: string;
}

export interface BulkCreateResult {
  created: number;
  errors: BulkRowError[];
}

export interface CreateOfferRequest {
  title: string;
  description: string;
  country: string;
  city: string;
  price: number;
  currency: string;
  boardBasis: string;
  transport: string;
  durationNights: number;
  startDate?: string | null;
  endDate?: string | null;
  tags?: string[];
  imageUrl?: string | null;
}

export interface FilterCondition {
  field: string;
  op: string;
  value: unknown;
}

export interface FilterSpec {
  all?: FilterCondition[];
  any?: FilterCondition[];
}

export interface SortSpec {
  field: string;
  direction: string;
}

export interface CollectionDto {
  id: string;
  agencyId: string;
  name: string;
  slug: string;
  status: string;
  filter?: FilterSpec;
  sort?: SortSpec;
}

export interface CreateCollectionRequest {
  agencyId: string;
  name: string;
  slug: string;
  filter: FilterSpec;
  sort?: SortSpec;
}

export interface ExperienceConfig {
  type: string;
  columns: number;
  cardStyle: string;
  showPrice: boolean;
  inquiry: boolean;
  openNewTab: boolean;
}

export interface PublicationDto {
  id: string;
  agencyId: string;
  key: string;
  collectionId: string;
  themeId: string | null;
  experienceConfig: ExperienceConfig | null;
  status: string;
  allowedDomains: string[];
  version: number;
  createdAt: string;
  updatedAt: string;
}

export interface CreatePublicationRequest {
  agencyId: string;
  collectionId: string;
  themeId?: string;
  experienceConfig?: ExperienceConfig;
  allowedDomains?: string[];
}

export interface ThemeTokens {
  foundation: Record<string, string>;
  semantic:   Record<string, string>;
  component:  Record<string, string>;
}

export interface ThemeDto {
  id:       string;
  agencyId: string;
  name:     string;
  status:   string;
  version:  number;
  tokens:   ThemeTokens;
  isPreset?: boolean;
}

export interface CreateThemeRequest {
  agencyId: string;
  name:     string;
  tokens?:  ThemeTokens;
}

export interface UpdateThemeRequest {
  name?:   string;
  tokens?: ThemeTokens;
}

export interface WebhookSubscriptionDto {
  id: string;
  agencyId: string;
  url: string;
  eventTypes: string;
  status: string;
  createdAt: string;
}

// Returned ONCE at creation — carries the signing secret, which is never echoed again.
export interface CreatedWebhookSubscriptionDto {
  id: string;
  url: string;
  eventTypes: string;
  secret: string;
}

export interface LeadDto {
  id: string;
  kind: string;
  status: string;
  agencyId: string;
  publicationKey: string;
  channel: string;
  contactName: string;
  contactEmail: string;
  contactPhone: string | null;
  message: string | null;
  offerId: string | null;
  partySize: number | null;
  preferredDepartureDate: string | null;
  nights: number | null;
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);
  private base = environment.apiBase;

  listOffers(): Observable<OfferDto[]> {
    return this.http.get<OfferDto[]>(`${this.base}/offers`);
  }

  // ── Operator-scoped offer management (OperatorAdmin) ──────────────────────────

  listMyOffers(): Observable<OfferDto[]> {
    return this.http.get<OfferDto[]>(`${this.base}/offers/mine`);
  }

  createOffer(req: CreateOfferRequest): Observable<OfferDto> {
    return this.http.post<OfferDto>(`${this.base}/offers`, req);
  }

  bulkCreateOffers(offers: CreateOfferRequest[]): Observable<BulkCreateResult> {
    return this.http.post<BulkCreateResult>(`${this.base}/offers/bulk`, { offers });
  }

  updateOffer(id: string, req: CreateOfferRequest): Observable<OfferDto> {
    return this.http.put<OfferDto>(`${this.base}/offers/${id}`, req);
  }

  publishOffer(id: string): Observable<OfferDto> {
    return this.http.post<OfferDto>(`${this.base}/offers/${id}/publish`, {});
  }

  unpublishOffer(id: string): Observable<OfferDto> {
    return this.http.post<OfferDto>(`${this.base}/offers/${id}/unpublish`, {});
  }

  listCollections(): Observable<CollectionDto[]> {
    return this.http.get<CollectionDto[]>(`${this.base}/collections`);
  }

  createCollection(req: CreateCollectionRequest): Observable<CollectionDto> {
    return this.http.post<CollectionDto>(`${this.base}/collections`, req);
  }

  resolveCollection(idOrSlug: string): Observable<OfferDto[]> {
    return this.http.get<OfferDto[]>(`${this.base}/collections/${idOrSlug}/offers`);
  }

  listPublications(): Observable<PublicationDto[]> {
    return this.http.get<PublicationDto[]>(`${this.base}/publications`);
  }

  createPublication(req: CreatePublicationRequest): Observable<PublicationDto> {
    return this.http.post<PublicationDto>(`${this.base}/publications`, req);
  }

  publishPublication(id: string): Observable<PublicationDto> {
    return this.http.post<PublicationDto>(`${this.base}/publications/${id}/publish`, {});
  }

  // ── Leads inbox (AgencyMember) ────────────────────────────────────────────────

  listLeads(status?: string): Observable<LeadDto[]> {
    const params = status ? `?status=${status}` : '';
    return this.http.get<LeadDto[]>(`${this.base}/leads${params}`);
  }

  setLeadStatus(id: string, status: string): Observable<LeadDto> {
    return this.http.post<LeadDto>(`${this.base}/leads/${id}/status`, { status });
  }

  // ── Webhook subscriptions (AgencyAdmin) ───────────────────────────────────────

  listWebhooks(): Observable<WebhookSubscriptionDto[]> {
    return this.http.get<WebhookSubscriptionDto[]>(`${this.base}/webhooks`);
  }

  createWebhook(url: string, eventTypes: string): Observable<CreatedWebhookSubscriptionDto> {
    return this.http.post<CreatedWebhookSubscriptionDto>(`${this.base}/webhooks`, { url, eventTypes });
  }

  enableWebhook(id: string): Observable<WebhookSubscriptionDto> {
    return this.http.post<WebhookSubscriptionDto>(`${this.base}/webhooks/${id}/enable`, {});
  }

  disableWebhook(id: string): Observable<WebhookSubscriptionDto> {
    return this.http.post<WebhookSubscriptionDto>(`${this.base}/webhooks/${id}/disable`, {});
  }

  deleteWebhook(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/webhooks/${id}`);
  }

  listThemes(agencyId?: string): Observable<ThemeDto[]> {
    const params = agencyId ? `?agencyId=${agencyId}` : '';
    return this.http.get<ThemeDto[]>(`${this.base}/themes${params}`);
  }

  getTheme(id: string): Observable<ThemeDto> {
    return this.http.get<ThemeDto>(`${this.base}/themes/${id}`);
  }

  listPresets(): Observable<ThemeDto[]> {
    return this.http.get<ThemeDto[]>(`${this.base}/themes/presets`);
  }

  cloneFromPreset(presetId: string, name?: string): Observable<ThemeDto> {
    return this.http.post<ThemeDto>(`${this.base}/themes/from-preset/${presetId}`, { name: name ?? null });
  }

  createTheme(req: CreateThemeRequest): Observable<ThemeDto> {
    return this.http.post<ThemeDto>(`${this.base}/themes`, req);
  }

  updateTheme(id: string, req: UpdateThemeRequest): Observable<ThemeDto> {
    return this.http.put<ThemeDto>(`${this.base}/themes/${id}`, req);
  }

  publishTheme(id: string): Observable<ThemeDto> {
    return this.http.post<ThemeDto>(`${this.base}/themes/${id}/publish`, {});
  }

  exportTheme(id: string, format: 'css' | 'json'): Observable<string> {
    return this.http.get(`${this.base}/themes/${id}/export?format=${format}`, { responseType: 'text' });
  }
}
