/** One parsed server-sent event. */
export interface SseMessage {
  event: string;
  data: string;
}

/**
 * Incremental parser for a `text/event-stream` body that arrives as a growing string
 * (Angular's `HttpDownloadProgressEvent.partialText` is the whole body received so far).
 * Each call returns only the events completed since the previous call; a half-received
 * event stays buffered until its terminating blank line arrives.
 */
export class SseParser {
  private consumed = 0;

  /**
   * Parses newly received text.
   * @param fullText The entire body received so far.
   * @returns Events completed since the last call, in order.
   */
  feed(fullText: string): SseMessage[] {
    const pending = fullText.slice(this.consumed).replace(/\r\n/g, '\n');
    const messages: SseMessage[] = [];

    let start = 0;
    let end: number;
    while ((end = pending.indexOf('\n\n', start)) !== -1) {
      const message = parseBlock(pending.slice(start, end));
      if (message) messages.push(message);
      start = end + 2;
    }

    // Advance by what was consumed in the ORIGINAL text, so CRLF bodies stay aligned.
    this.consumed += originalLength(fullText.slice(this.consumed), start);
    return messages;
  }
}

/** Parses one event block (`event:` / `data:` lines). @param block Raw block text. @returns The event, or null for comments/empty blocks. */
function parseBlock(block: string): SseMessage | null {
  let event = 'message';
  const data: string[] = [];

  for (const line of block.split('\n')) {
    if (line.startsWith(':')) continue;
    const colon = line.indexOf(':');
    const field = colon === -1 ? line : line.slice(0, colon);
    const value = colon === -1 ? '' : line.slice(colon + 1).replace(/^ /, '');
    if (field === 'event') event = value;
    else if (field === 'data') data.push(value);
  }

  return data.length === 0 ? null : { event, data: data.join('\n') };
}

/**
 * Maps a length measured on the LF-normalized text back to the original text.
 * @param original Unconsumed original text (may contain CRLF).
 * @param normalizedLength Characters consumed in the normalized version.
 * @returns Characters to consume in the original.
 */
function originalLength(original: string, normalizedLength: number): number {
  let o = 0;
  let n = 0;
  while (n < normalizedLength && o < original.length) {
    if (original[o] === '\r' && original[o + 1] === '\n') o++;
    o++;
    n++;
  }
  return o;
}
