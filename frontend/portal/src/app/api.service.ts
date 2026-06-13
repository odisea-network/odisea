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

  listThemes(agencyId?: string): Observable<ThemeDto[]> {
    const params = agencyId ? `?agencyId=${agencyId}` : '';
    return this.http.get<ThemeDto[]>(`${this.base}/themes${params}`);
  }

  getTheme(id: string): Observable<ThemeDto> {
    return this.http.get<ThemeDto>(`${this.base}/themes/${id}`);
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
