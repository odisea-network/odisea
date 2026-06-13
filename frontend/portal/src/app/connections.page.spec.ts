import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiService, ConnectionHealthDto, ImportJobDto } from './api.service';
import { ConnectionsPage } from './connections.page';

const health = (id: string, over: Partial<ConnectionHealthDto> = {}): ConnectionHealthDto => ({
  supplierConnectionId: id, name: `Conn ${id}`, kind: 'JsonApi', status: 'Active',
  lastSyncedAt: new Date().toISOString(), lastSuccessfulRunAt: null,
  lastRunStatus: 'Succeeded', recentRuns: 3, recentFailures: 0, ...over,
});

const job = (id: string): ImportJobDto => ({
  id, supplierConnectionId: 'c1', status: 'Succeeded', startedAt: new Date().toISOString(),
  completedAt: null, offersFetched: 5, offersImported: 5, offersDeactivated: 0, errors: [],
});

function makeApi(over: Partial<Record<keyof ApiService, unknown>> = {}) {
  return {
    supplierHealth: vi.fn().mockReturnValue(of([health('c1'), health('c2', { status: 'Paused' })])),
    runConnection: vi.fn().mockReturnValue(of(job('j1'))),
    sweepConnection: vi.fn().mockReturnValue(of({ sourceOffersMarkedStale: 2, offersMarkedStale: 1 })),
    connectionJobs: vi.fn().mockReturnValue(of([job('j1'), job('j2')])),
    ...over,
  } as unknown as ApiService;
}

function make(api: ApiService): ConnectionsPage {
  TestBed.configureTestingModule({ providers: [{ provide: ApiService, useValue: api }] });
  return TestBed.runInInjectionContext(() => new ConnectionsPage());
}

describe('ConnectionsPage', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('loads supplier health on construction', () => {
    const api = makeApi();
    const page = make(api);

    expect(api.supplierHealth).toHaveBeenCalled();
    expect(page.health().length).toBe(2);
    expect(page.loading()).toBe(false);
  });

  it('run triggers the connector, shows a notice, and reloads health', () => {
    const api = makeApi();
    const page = make(api);

    page.run(health('c1'));

    expect(api.runConnection).toHaveBeenCalledWith('c1');
    expect(page.notice()).toContain('5 imported');
    expect(api.supplierHealth).toHaveBeenCalledTimes(2); // initial + reload
    expect(page.busy().has('c1')).toBe(false);
  });

  it('sweep reports how many offers were marked stale', () => {
    const api = makeApi();
    const page = make(api);

    page.sweep(health('c1'));

    expect(api.sweepConnection).toHaveBeenCalledWith('c1');
    expect(page.notice()).toContain('1 offer(s) marked stale');
  });

  it('toggleJobs expands and loads run history, then collapses', () => {
    const api = makeApi();
    const page = make(api);

    page.toggleJobs(health('c1'));
    expect(page.expandedId()).toBe('c1');
    expect(api.connectionJobs).toHaveBeenCalledWith('c1');
    expect(page.jobs().length).toBe(2);

    page.toggleJobs(health('c1'));
    expect(page.expandedId()).toBeNull();
  });

  it('isStale flags syncs older than 24h', () => {
    const page = make(makeApi());
    const old = new Date(Date.now() - 25 * 60 * 60 * 1000).toISOString();
    const fresh = new Date().toISOString();

    expect(page.isStale(old)).toBe(true);
    expect(page.isStale(fresh)).toBe(false);
  });

  it('surfaces an error when health fails to load', () => {
    const api = makeApi({
      supplierHealth: vi.fn().mockReturnValue(throwError(() => ({ error: { detail: 'boom' } }))),
    });
    const page = make(api);

    expect(page.error()).toBe('boom');
    expect(page.loading()).toBe(false);
  });
});
