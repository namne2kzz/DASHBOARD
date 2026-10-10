import { SseParser } from './sse-parser';

/**
 * Unit tests for SseParser. The body arrives as an ever-growing string (HttpClient's partialText),
 * so the parser must emit each event exactly once, hold back half-received events, and cope with
 * CRLF bodies and multi-line data.
 */
describe('SseParser', () => {
  it('emits each completed event once as the body grows', () => {
    const parser = new SseParser();
    const part1 = 'event: meta\ndata: {"conversationId":"c1"}\n\nevent: delta\ndata: {"te';
    const part2 = part1 + 'xt":"Xin"}\n\n';

    expect(parser.feed(part1)).toEqual([{ event: 'meta', data: '{"conversationId":"c1"}' }]);
    expect(parser.feed(part2)).toEqual([{ event: 'delta', data: '{"text":"Xin"}' }]);
    expect(parser.feed(part2)).toEqual([]);
  });

  it('handles CRLF line endings', () => {
    const parser = new SseParser();
    const body = 'event: delta\r\ndata: {"text":"a"}\r\n\r\nevent: done\r\ndata: {}\r\n\r\n';

    expect(parser.feed(body).map(m => m.event)).toEqual(['delta', 'done']);
    expect(parser.feed(body + 'event: delta\r\ndata: {"text":"b"}\r\n\r\n')).toEqual([{ event: 'delta', data: '{"text":"b"}' }]);
  });

  it('joins multi-line data and ignores comments', () => {
    const parser = new SseParser();

    const messages = parser.feed(': keep-alive\n\ndata: line1\ndata: line2\n\n');

    expect(messages).toEqual([{ event: 'message', data: 'line1\nline2' }]);
  });
});
