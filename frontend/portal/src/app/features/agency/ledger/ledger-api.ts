import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  CreateLedgerReservationRequest, InstallmentRequest, LedgerAuditEventDto, LedgerDashboardDto, LedgerDocumentDto,
  LedgerDocumentType, LedgerReservationDto, LedgerReservationStatus, LedgerReservationSummaryDto, PagedResult,
  RecordLedgerPaymentRequest, SaveTourOperatorRequest, TourOperatorDto, UpdateLedgerReservationRequest,
} from '../../../core/api.types';

export interface LedgerListQuery {
  q: string;
  status: LedgerReservationStatus | '';
  operatorId: string;
  dueWithinDays: number | null;
  overdueOnly: boolean;
  page: number;
  pageSize: number;
}

const RESERVATIONS = '/api/v1/agency/ledger/reservations';
const OPERATORS = '/api/v1/agency/tour-operators';

/** The ledger endpoints. The agency is never sent: the API takes it from the session. */
@Injectable({ providedIn: 'root' })
export class LedgerApi {
  private readonly http = inject(HttpClient);

  dashboard(): Promise<LedgerDashboardDto> {
    return firstValueFrom(this.http.get<LedgerDashboardDto>('/api/v1/agency/ledger/dashboard'));
  }

  list(query: LedgerListQuery): Promise<PagedResult<LedgerReservationSummaryDto>> {
    let params = new HttpParams().set('page', query.page).set('pageSize', query.pageSize);
    if (query.q.trim()) params = params.set('q', query.q.trim());
    if (query.status) params = params.set('status', query.status);
    if (query.operatorId) params = params.set('operatorId', query.operatorId);
    if (query.dueWithinDays !== null) params = params.set('dueWithinDays', query.dueWithinDays);
    if (query.overdueOnly) params = params.set('overdueOnly', true);
    return firstValueFrom(this.http.get<PagedResult<LedgerReservationSummaryDto>>(RESERVATIONS, { params }));
  }

  get(id: string): Promise<LedgerReservationDto> {
    return firstValueFrom(this.http.get<LedgerReservationDto>(`${RESERVATIONS}/${id}`));
  }

  create(request: CreateLedgerReservationRequest): Promise<LedgerReservationDto> {
    return firstValueFrom(this.http.post<LedgerReservationDto>(RESERVATIONS, request));
  }

  update(id: string, request: UpdateLedgerReservationRequest): Promise<LedgerReservationDto> {
    return firstValueFrom(this.http.put<LedgerReservationDto>(`${RESERVATIONS}/${id}`, request));
  }

  replaceSchedule(
    id: string, version: number, installments: InstallmentRequest[], reason: string | null,
  ): Promise<LedgerReservationDto> {
    return firstValueFrom(
      this.http.put<LedgerReservationDto>(`${RESERVATIONS}/${id}/schedule`, { version, installments, reason }));
  }

  recordPayment(id: string, request: RecordLedgerPaymentRequest): Promise<LedgerReservationDto> {
    return firstValueFrom(this.http.post<LedgerReservationDto>(`${RESERVATIONS}/${id}/payments`, request));
  }

  voidPayment(id: string, paymentId: string, reason: string): Promise<LedgerReservationDto> {
    return firstValueFrom(
      this.http.post<LedgerReservationDto>(`${RESERVATIONS}/${id}/payments/${paymentId}/void`, { reason }));
  }

  changeStatus(
    id: string, version: number, status: LedgerReservationStatus, note: string | null,
  ): Promise<LedgerReservationDto> {
    return firstValueFrom(this.http.post<LedgerReservationDto>(`${RESERVATIONS}/${id}/status`, { version, status, note }));
  }

  setCommissionReceived(id: string, version: number, receivedAmount: number): Promise<LedgerReservationDto> {
    return firstValueFrom(
      this.http.put<LedgerReservationDto>(`${RESERVATIONS}/${id}/commission`, { version, receivedAmount }));
  }

  history(id: string): Promise<LedgerAuditEventDto[]> {
    return firstValueFrom(this.http.get<LedgerAuditEventDto[]>(`${RESERVATIONS}/${id}/history`));
  }

  documents(id: string): Promise<LedgerDocumentDto[]> {
    return firstValueFrom(this.http.get<LedgerDocumentDto[]>(`${RESERVATIONS}/${id}/documents`));
  }

  uploadDocument(id: string, file: File, type: LedgerDocumentType): Promise<LedgerDocumentDto> {
    const form = new FormData();
    form.append('file', file, file.name);
    form.append('type', type);
    return firstValueFrom(this.http.post<LedgerDocumentDto>(`${RESERVATIONS}/${id}/documents`, form));
  }

  /** Fetched through HttpClient so the session token applies; there is no public link to a file. */
  documentFile(id: string, documentId: string): Promise<Blob> {
    return firstValueFrom(this.http.get(`${RESERVATIONS}/${id}/documents/${documentId}/file`, { responseType: 'blob' }));
  }

  deleteDocument(id: string, documentId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${RESERVATIONS}/${id}/documents/${documentId}`));
  }

  operators(includeArchived = false): Promise<TourOperatorDto[]> {
    return firstValueFrom(this.http.get<TourOperatorDto[]>(OPERATORS, { params: { includeArchived } }));
  }

  createOperator(request: SaveTourOperatorRequest): Promise<TourOperatorDto> {
    return firstValueFrom(this.http.post<TourOperatorDto>(OPERATORS, request));
  }

  updateOperator(id: string, request: SaveTourOperatorRequest): Promise<TourOperatorDto> {
    return firstValueFrom(this.http.put<TourOperatorDto>(`${OPERATORS}/${id}`, request));
  }

  setOperatorArchived(id: string, archived: boolean): Promise<TourOperatorDto> {
    return firstValueFrom(this.http.post<TourOperatorDto>(`${OPERATORS}/${id}/${archived ? 'archive' : 'unarchive'}`, {}));
  }
}
