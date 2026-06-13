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

const connectionDto = (id: string, configJson: string) => ({
  id, operatorId: 'op', kind: 'JsonApi', name: `Conn ${id}`, status: 'Active',
  configJson, freshnessTtlHours: 24, lastSyncedAt: null, createdAt: new Date().toISOString(),
});

function makeApi(over: Partial<Record<keyof ApiService, unknown>> = {}) {
  return {
    supplierHealth: vi.fn().mockReturnValue(of([health('c1'), health('c2', { status: 'Paused' })])),
    runConnection: vi.fn().mockReturnValue(of(job('j1'))),
    sweepConnection: vi.fn().mockReturnValue(of({ sourceOffersMarkedStale: 2, offersMarkedStale: 1 })),
    connectionJobs: vi.fn().mockReturnValue(of([job('j1'), job('j2')])),
    getConnection: vi.fn().mockReturnValue(of(connectionDto('c1', '{"url":"https://x","fieldMap":{"externalId":"id"}}'))),
    createConnection: vi.fn().mockReturnValue(of(connectionDto('c3', '{}'))),
    updateConnection: vi.fn().mockReturnValue(of(connectionDto('c1', '{}'))),
    deleteConnection: vi.fn().mockReturnValue(of(undefined)),
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

  it('newConnection opens an empty create form', () => {
    const page = make(makeApi());
    page.newConnection();

    expect(page.showForm()).toBe(true);
    expect(page.editingId()).toBeNull();
    expect(page.form.name).toBe('');
    expect(page.form.kind).toBe('JsonApi');
  });

  it('save in create mode posts a built configJson and reloads', () => {
    const api = makeApi();
    const page = make(api);
    page.newConnection();
    page.form.name = 'Nightly';
    page.form.url = 'https://supplier/feed';
    page.form.fieldMap = [{ canonical: 'externalId', supplier: 'id' }];

    page.save();

    expect(api.createConnection).toHaveBeenCalledWith(
      expect.objectContaining({
        kind: 'JsonApi',
        name: 'Nightly',
        configJson: JSON.stringify({ url: 'https://supplier/feed', fieldMap: { externalId: 'id' } }),
      }),
    );
    expect(page.showForm()).toBe(false);
    expect(api.supplierHealth).toHaveBeenCalledTimes(2); // initial + reload
  });

  it('editConnection loads the dto and parses its config into the form', () => {
    const api = makeApi();
    const page = make(api);

    page.editConnection(health('c1'));

    expect(api.getConnection).toHaveBeenCalledWith('c1');
    expect(page.editingId()).toBe('c1');
    expect(page.form.url).toBe('https://x');
    expect(page.form.fieldMap).toEqual([{ canonical: 'externalId', supplier: 'id' }]);
  });

  it('save in edit mode calls updateConnection', () => {
    const api = makeApi();
    const page = make(api);
    page.editConnection(health('c1'));
    page.form.name = 'Renamed';

    page.save();

    expect(api.updateConnection).toHaveBeenCalledWith('c1', expect.objectContaining({ name: 'Renamed' }));
  });

  it('addRow and removeRow edit the field-map list', () => {
    const page = make(makeApi());
    page.newConnection();
    page.addRow();
    page.addRow();
    expect(page.form.fieldMap.length).toBe(2);
    page.removeRow(0);
    expect(page.form.fieldMap.length).toBe(1);
  });

  it('remove deletes after confirmation and reloads', () => {
    const api = makeApi();
    const page = make(api);
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    page.remove(health('c1'));

    expect(api.deleteConnection).toHaveBeenCalledWith('c1');
    expect(api.supplierHealth).toHaveBeenCalledTimes(2);
  });

  it('remove does nothing when confirmation is cancelled', () => {
    const api = makeApi();
    const page = make(api);
    vi.spyOn(window, 'confirm').mockReturnValue(false);

    page.remove(health('c1'));

    expect(api.deleteConnection).not.toHaveBeenCalled();
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
