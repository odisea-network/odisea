import { describe, expect, it } from 'vitest';
import { apiKeyState, keyExpiresAt } from './agency-detail';

describe('apiKeyState', () => {
  const now = new Date('2026-10-26T12:00:00Z');

  it('is revoked before anything else', () => {
    expect(apiKeyState({ revokedAt: '2026-10-01T00:00:00Z', expiresAt: '2030-01-01T00:00:00Z' }, now)).toBe('Revoked');
  });

  it('is expired once the expiry has passed, active otherwise', () => {
    expect(apiKeyState({ revokedAt: null, expiresAt: '2026-10-25T00:00:00Z' }, now)).toBe('Expired');
    expect(apiKeyState({ revokedAt: null, expiresAt: '2026-10-27T00:00:00Z' }, now)).toBe('Active');
    expect(apiKeyState({ revokedAt: null, expiresAt: null }, now)).toBe('Active');
  });
});

describe('keyExpiresAt', () => {
  const now = new Date('2026-10-26T12:00:00Z');

  it('maps the choices to an instant, or to no expiry', () => {
    expect(keyExpiresAt('90d', now)).toBe('2027-01-24T12:00:00.000Z');
    expect(keyExpiresAt('1y', now)).toBe('2027-10-26T12:00:00.000Z');
    expect(keyExpiresAt('never', now)).toBeNull();
  });
});
