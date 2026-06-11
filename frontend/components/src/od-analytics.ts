/**
 * Lightweight analytics beacon helper shared by od-* components.
 *
 * Sends events to the Odisea ingest endpoint with `navigator.sendBeacon` (which
 * survives page unload), falling back to `fetch` with `keepalive`. The endpoint
 * defaults to same-origin `/api/v1/events`; override globally via
 * `window.OD_ANALYTICS_ENDPOINT`.
 *
 * Domain event types: "Impression" | "Open" | "InquiryStart" | "InquirySubmit".
 */

export type OdEventType = 'Impression' | 'Open' | 'InquiryStart' | 'InquirySubmit';

export interface OdAnalyticsEvent {
  eventType: OdEventType;
  publicationKey: string;
  offerId?: string;
  channel?: string;
  occurredAt?: string;
}

declare global {
  interface Window {
    OD_ANALYTICS_ENDPOINT?: string;
  }
}

function endpoint(): string {
  return (typeof window !== 'undefined' && window.OD_ANALYTICS_ENDPOINT) || '/api/v1/events';
}

/**
 * Fires a single analytics event. No-op when `publicationKey` is empty —
 * without attribution the event is meaningless and the backend drops it anyway.
 */
export function sendOdEvent(event: OdAnalyticsEvent): void {
  if (!event.publicationKey) return;

  const payload = {
    events: [
      {
        eventType: event.eventType,
        publicationKey: event.publicationKey,
        offerId: event.offerId,
        channel: event.channel ?? 'WebComponent',
        occurredAt: event.occurredAt ?? new Date().toISOString(),
      },
    ],
  };

  const url = endpoint();
  const body = JSON.stringify(payload);

  try {
    if (typeof navigator !== 'undefined' && typeof navigator.sendBeacon === 'function') {
      navigator.sendBeacon(url, new Blob([body], { type: 'application/json' }));
      return;
    }
  } catch {
    // fall through to fetch
  }

  if (typeof fetch === 'function') {
    fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body,
      keepalive: true,
    }).catch(() => { /* analytics is best-effort — never surface errors */ });
  }
}
