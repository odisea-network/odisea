/**
 * Resolves the offers URL for an embed component from one of three sources, in
 * priority order:
 *
 *   1. `endpoint`     — a full URL, used verbatim.
 *   2. `publication`  — a stable publication key. The manifest flow: fetch
 *                       `{apiBase}/api/v1/publications/{key}`, then use its
 *                       `offersUrl` (an id-based, tenant-safe path). This is the
 *                       supported embed path — publication keys are public and
 *                       anonymous-safe, whereas collection slugs are unique only
 *                       per agency (#18) and 404 for anonymous callers.
 *   3. `collection`   — a collection slug, kept for backwards compatibility.
 *                       Only resolves for authenticated, agency-scoped callers;
 *                       anonymous embeds should use `publication` instead.
 *
 * Returns an empty string when no source is set.
 */
export interface OfferSource {
  endpoint?: string;
  publication?: string;
  collection?: string;
}

interface Manifest {
  offersUrl: string;
}

/**
 * Builds the Authorization header for a publishable embed key. The manifest and
 * offers endpoints require `Authorization: ApiKey <key>`; an unset key yields an
 * empty header set (so same-origin/authenticated contexts still work).
 */
export function authHeaders(apiKey?: string): Record<string, string> {
  return apiKey ? { Authorization: `ApiKey ${apiKey}` } : {};
}

export async function resolveOffersUrl(
  apiBase: string,
  src: OfferSource,
  apiKey?: string,
): Promise<string> {
  if (src.endpoint) return src.endpoint;

  if (src.publication) {
    const manifestUrl = `${apiBase}/api/v1/publications/${encodeURIComponent(src.publication)}`;
    const res = await fetch(manifestUrl, { headers: authHeaders(apiKey) });
    if (!res.ok) throw new Error(`manifest HTTP ${res.status}`);
    const manifest = (await res.json()) as Manifest;
    // offersUrl is a relative path (e.g. /api/v1/collections/{id}/offers).
    return `${apiBase}${manifest.offersUrl}`;
  }

  if (src.collection) {
    return `${apiBase}/api/v1/collections/${encodeURIComponent(src.collection)}/offers`;
  }

  return '';
}

/** True when at least one offer source is configured. */
export function hasOfferSource(src: OfferSource): boolean {
  return Boolean(src.endpoint || src.publication || src.collection);
}
