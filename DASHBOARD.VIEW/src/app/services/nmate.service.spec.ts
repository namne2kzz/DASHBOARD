import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { HttpDownloadProgressEvent, HttpEventType, provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { NMateService } from './nmate.service';
import { AuthService } from './auth.service';
import { RepositoryContextService } from './repository-context.service';
import { environment } from '../../environments/environment';

/**
 * Unit tests for NMateService — the widget's state and its streamed calls.
 *
 * The answer arrives as SSE inside one HTTP response, surfaced by HttpClient as download-progress
 * events whose partialText grows; the tests replay that with HttpTestingController so the parsing,
 * the per-event state changes and the error mapping are all exercised without a server.
 */
describe('NMateService', () => {
  let service: NMateService;
  let httpMock: HttpTestingController;
  const base = `${environment.apiBaseUrl}/nmate`;

  const sse = (...events: [string, unknown][]) =>
    events.map(([name, data]) => `event: ${name}\ndata: ${JSON.stringify(data)}\n\n`).join('');

  beforeEach(() => {
    sessionStorage.removeItem('nmate.conversationId');
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { currentUser: signal({ orgAlias: 'acme' }) } },
        {
          provide: RepositoryContextService,
          useValue: { selectedRepoId: signal('repo-1'), selectedRepo: signal({ code: 'DASH' }) },
        },
      ],
    });
    service = TestBed.inject(NMateService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.removeItem('nmate.conversationId');
  });

  it('streams an answer: deltas append, citations and server id arrive at the end', () => {
    service.ask('Làm sao đóng sprint?', '/acme/DASH/sprint-planning');

    const req = httpMock.expectOne(`${base}/chat`);
    expect(req.request.body).toEqual({
      conversationId: null,
      message: 'Làm sao đóng sprint?',
      context: { route: '/acme/DASH/sprint-planning', repositoryId: 'repo-1' },
    });
    expect(service.isStreaming()).toBeTrue();

    const first = sse(['meta', { conversationId: 'c1' }], ['delta', { text: 'Bấm ' }]);
    req.event({ type: HttpEventType.DownloadProgress, loaded: 1, partialText: first } as HttpDownloadProgressEvent);

    let answer = service.messages()[1];
    expect(answer.content).toBe('Bấm ');
    expect(answer.status).toBe('streaming');
    expect(service.conversationId()).toBe('c1');
    expect(sessionStorage.getItem('nmate.conversationId')).toBe('c1');

    const all = first + sse(
      ['delta', { text: '**Close**.' }],
      ['citations', [{ chunkId: 'k1', title: 'Quản lý Sprint', headingPath: 'Đóng sprint', route: '/sprint-planning', score: 0.8 }]],
      ['done', { messageId: 'm1', latencyMs: 900, answered: true }],
    );
    req.flush(all);

    answer = service.messages()[1];
    expect(answer.content).toBe('Bấm **Close**.');
    expect(answer.citations.length).toBe(1);
    expect(answer.id).toBe('m1');
    expect(answer.status).toBe('done');
    expect(service.isStreaming()).toBeFalse();
  });

  it('marks a declined answer when NMate had no documentation', () => {
    service.ask('Thời tiết?', null);

    httpMock.expectOne(`${base}/chat`).flush(sse(
      ['meta', { conversationId: 'c1' }],
      ['delta', { text: 'Mình chưa có tài liệu…' }],
      ['done', { messageId: 'm2', latencyMs: 10, answered: false }],
    ));

    expect(service.messages()[1].status).toBe('declined');
  });

  it('keeps the partial answer and the code when the stream reports an error', () => {
    service.ask('Hỏi', null);

    httpMock.expectOne(`${base}/chat`).flush(sse(
      ['delta', { text: 'Phần đầu' }],
      ['error', { code: 'NMATE_QUOTA_EXCEEDED', message: 'x', messageId: 'm3' }],
    ));

    const answer = service.messages()[1];
    expect(answer.content).toBe('Phần đầu');
    expect(answer.status).toBe('error');
    expect(answer.errorCode).toBe('NMATE_QUOTA_EXCEEDED');
  });

  it('maps HTTP failures to error codes: 429 rate limit, 503 body code, 404 disables the widget', () => {
    service.ask('a', null);
    httpMock.expectOne(`${base}/chat`).flush('', { status: 429, statusText: 'Too Many Requests' });
    expect(service.messages()[1].errorCode).toBe('RATE_LIMITED');

    service.ask('b', null);
    httpMock.expectOne(`${base}/chat`).flush('{"error":"NMATE_UNAVAILABLE"}', { status: 503, statusText: 'Unavailable' });
    expect(service.messages()[3].errorCode).toBe('NMATE_UNAVAILABLE');

    service.ask('c', null);
    httpMock.expectOne(`${base}/chat`).flush('', { status: 404, statusText: 'Not Found' });
    expect(service.enabled()).toBeFalse();
  });

  it('ignores a new question while an answer is streaming', () => {
    service.ask('một', null);
    service.ask('hai', null);

    httpMock.expectOne(`${base}/chat`).flush(sse(['done', { messageId: 'm', latencyMs: 1, answered: true }]));
    expect(service.messages().filter(m => m.role === 'user').length).toBe(1);
  });

  it('stop() aborts the request and marks the answer interrupted', () => {
    service.ask('Hỏi', null);
    const req = httpMock.expectOne(`${base}/chat`);

    service.stop();

    expect(req.cancelled).toBeTrue();
    expect(service.messages()[1].errorCode).toBe('INTERRUPTED');
    expect(service.isStreaming()).toBeFalse();
  });

  it('rate() is optimistic and reverts when the call fails', () => {
    service.ask('Hỏi', null);
    httpMock.expectOne(`${base}/chat`).flush(sse(['done', { messageId: 'm1', latencyMs: 1, answered: true }]));
    const answer = service.messages()[1];

    service.rate(answer, 1);
    expect(service.messages()[1].rating).toBe(1);

    const put = httpMock.expectOne(`${base}/messages/m1/feedback`);
    expect(put.request.body).toEqual({ rating: 1 });
    put.flush('', { status: 500, statusText: 'Error' });

    expect(service.messages()[1].rating).toBeNull();
  });

  it('status 404 means the feature is off; status available=false means maintenance', () => {
    service.refreshStatus();
    httpMock.expectOne(`${base}/status`).flush('', { status: 404, statusText: 'Not Found' });
    expect(service.enabled()).toBeFalse();

    service.refreshStatus();
    httpMock.expectOne(`${base}/status`).flush({ available: false, chatModel: 'g', activeDocuments: 0, lastIngestionAt: null });
    expect(service.enabled()).toBeTrue();
    expect(service.available()).toBeFalse();
  });

  it('builds citation links under the current org and repository', () => {
    expect(service.citationLink('/sprint-planning')).toBe('/acme/DASH/sprint-planning');
    expect(service.citationLink('/settings/members')).toBe('/acme/settings/members');
    expect(service.citationLink(null)).toBeNull();
  });

  it('restores the previous conversation from sessionStorage on first open', () => {
    sessionStorage.setItem('nmate.conversationId', 'c9');
    service.conversationId.set('c9');

    service.open();

    httpMock.expectOne(`${base}/status`).flush({ available: true, chatModel: 'g', activeDocuments: 3, lastIngestionAt: null });
    httpMock.expectOne(`${base}/conversations/c9/messages`).flush([
      { id: 'u1', role: 'user', content: 'Hỏi cũ', citations: [], isInterrupted: false, rating: null, createdAt: '' },
      { id: 'a1', role: 'assistant', content: 'Đáp cũ', citations: [], isInterrupted: false, rating: 1, createdAt: '' },
    ]);

    expect(service.messages().map(m => m.content)).toEqual(['Hỏi cũ', 'Đáp cũ']);
    expect(service.messages()[1].rating).toBe(1);
  });
});
