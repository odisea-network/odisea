import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';

export interface OfferDto {
  id: string;
  title: string;
  country: string;
  city: string;
  price: number;
  currency: string;
  boardBasis: string;
  transport: string;
  durationNights: number;
  imageUrl: string;
  visibility: string;
}

export interface CollectionDto {
  id: string;
  agencyId: string;
  name: string;
  slug: string;
  status: string;
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

  listCollections(): Observable<CollectionDto[]> {
    return this.http.get<CollectionDto[]>(`${this.base}/collections`);
  }

  resolveCollection(idOrSlug: string): Observable<OfferDto[]> {
    return this.http.get<OfferDto[]>(`${this.base}/collections/${idOrSlug}/offers`);
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
