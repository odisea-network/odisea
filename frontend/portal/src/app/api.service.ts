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
}
