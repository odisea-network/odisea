import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiService, WebhookSubscriptionDto } from './api.service';
import { WebhooksPage } from './webhooks.page';

const sub = (id: string, status: string): WebhookSubscriptionDto => ({
  id, agencyId: 'ag-1', url: `https://x/${id}`, eventTypes: 'lead.created', status, createdAt: '2026-06-13T10:00:00Z',
});

function setup(api: Partial<Record<keyof ApiService, unknown>> = {}) {
  const apiMock = {
    listWebhooks: vi.fn().mockReturnValue(of([sub('1', 'Active'), sub('2', 'Disabled')])),
    createWebhook: vi.fn().mockReturnValue(of({ id: '3', url: 'https://x/3', eventTypes: 'lead.created', secret: 'sek_abc' })),
    enableWebhook: vi.fn().mockReturnValue(of(sub('2', 'Active'))),
    disableWebhook: vi.fn().mockReturnValue(of(sub('1', 'Disabled'))),
    deleteWebhook: vi.fn().mockReturnValue(of(undefined)),
    ...api,
  } as unknown as ApiService;
  TestBed.configureTestingModule({ providers: [{ provide: ApiService, useValue: apiMock }] });
  const page = TestBed.runInInjectionContext(() => new WebhooksPage());
  return { page, apiMock };
}

describe('WebhooksPage', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('loads subscriptions on construction', () => {
    const { page, apiMock } = setup();
    expect(apiMock.listWebhooks).toHaveBeenCalled();
    expect(page.subs().length).toBe(2);
  });

  it('toggle adds/removes an event from the selection', () => {
    const { page } = setup();
    page.toggle('publication.published');
    expect(page.selected.has('publication.published')).toBe(true);
    page.toggle('publication.published');
    expect(page.selected.has('publication.published')).toBe(false);
  });

  it('create posts the url + joined events and surfaces the one-time secret', () => {
    const { page, apiMock } = setup();
    page.url = 'https://crm/hook';
    page.selected = new Set(['lead.created', 'publication.published']);

    page.create();

    expect(apiMock.createWebhook).toHaveBeenCalledWith('https://crm/hook', 'lead.created,publication.published');
    expect(page.justCreated()?.secret).toBe('sek_abc');
    expect(apiMock.listWebhooks).toHaveBeenCalledTimes(2); // initial + reload
  });

  it('dismissSecret clears the secret card', () => {
    const { page } = setup();
    page.create();
    page.dismissSecret();
    expect(page.justCreated()).toBeNull();
  });

  it('setEnabled routes to the right endpoint', () => {
    const { page, apiMock } = setup();
    page.setEnabled(sub('1', 'Active'), false);
    expect(apiMock.disableWebhook).toHaveBeenCalledWith('1');
    page.setEnabled(sub('2', 'Disabled'), true);
    expect(apiMock.enableWebhook).toHaveBeenCalledWith('2');
  });

  it('remove deletes then reloads', () => {
    const { page, apiMock } = setup();
    page.remove(sub('1', 'Active'));
    expect(apiMock.deleteWebhook).toHaveBeenCalledWith('1');
  });

  it('surfaces a create error', () => {
    const { page } = setup({ createWebhook: vi.fn().mockReturnValue(throwError(() => ({ error: { detail: 'bad url' } }))) });
    page.url = 'nope';
    page.create();
    expect(page.error()).toBe('bad url');
    expect(page.saving()).toBe(false);
  });
});
