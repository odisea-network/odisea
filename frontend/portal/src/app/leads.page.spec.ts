import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiService, LeadDto } from './api.service';
import { LeadsPage } from './leads.page';

const lead = (id: string, status: string, kind = 'Inquiry'): LeadDto => ({
  id, kind, status, agencyId: 'ag-1', publicationKey: 'pub', channel: 'WebComponent',
  contactName: `Lead ${id}`, contactEmail: `${id}@example.bg`, contactPhone: null,
  message: 'Hi', offerId: null, partySize: null, preferredDepartureDate: null, nights: null,
  createdAt: '2026-06-13T10:00:00Z',
});

function makeApi(overrides: Partial<Record<keyof ApiService, unknown>> = {}) {
  return {
    listLeads: vi.fn().mockReturnValue(of([lead('1', 'New'), lead('2', 'Contacted')])),
    setLeadStatus: vi.fn().mockReturnValue(of(lead('1', 'Contacted'))),
    ...overrides,
  } as unknown as ApiService;
}

function make(api: ApiService): LeadsPage {
  TestBed.configureTestingModule({ providers: [{ provide: ApiService, useValue: api }] });
  return TestBed.runInInjectionContext(() => new LeadsPage());
}

describe('LeadsPage', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('loads leads with no filter on construction', () => {
    const api = makeApi();
    const page = make(api);

    expect(api.listLeads).toHaveBeenCalledWith(undefined);
    expect(page.leads().length).toBe(2);
    expect(page.loading()).toBe(false);
  });

  it('setFilter reloads with the chosen status', () => {
    const api = makeApi();
    const page = make(api);

    page.setFilter('Converted');

    expect(page.filter()).toBe('Converted');
    expect(api.listLeads).toHaveBeenLastCalledWith('Converted');
  });

  it('advance posts the new status then reloads', () => {
    const api = makeApi();
    const page = make(api);

    page.advance(lead('1', 'New'), 'Contacted');

    expect(api.setLeadStatus).toHaveBeenCalledWith('1', 'Contacted');
    expect(api.listLeads).toHaveBeenCalledTimes(2); // initial + reload
  });

  it('advance is a no-op when the status is unchanged', () => {
    const api = makeApi();
    const page = make(api);

    page.advance(lead('1', 'New'), 'New');

    expect(api.setLeadStatus).not.toHaveBeenCalled();
  });
});
