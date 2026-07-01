/** Wiki page IDs follow pattern W-101, W-102, … */
const WIKI_ID_PATTERN = /W-\d+/i;

/**
 * Extracts a wiki page id from a full URL, path, or raw id string.
 * Supports: /wiki/W-101, #/wiki/W-101, W-101
 */
export function parseWikiPageIdFromLink(link: string): string | null {
  const trimmed = link.trim();
  if (!trimmed) {
    return null;
  }
  const pathMatch = trimmed.match(/\/wiki\/(W-\d+)/i);
  if (pathMatch) {
    return pathMatch[1].toUpperCase();
  }
  const idMatch = trimmed.match(WIKI_ID_PATTERN);
  if (idMatch) {
    return idMatch[0].toUpperCase();
  }
  return null;
}

export function buildWikiPagePath(pageId: string): string {
  return `/wiki/${pageId.toUpperCase()}`;
}

export function buildWikiPageUrl(pageId: string, origin?: string): string {
  const path = buildWikiPagePath(pageId);
  const base = origin ?? (typeof window !== 'undefined' ? window.location.origin : '');
  return base ? `${base}${path}` : path;
}

export function isWikiLink(link: string): boolean {
  return parseWikiPageIdFromLink(link) !== null;
}
