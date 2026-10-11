import {
  HttpClient,
  HttpDownloadProgressEvent,
  HttpErrorResponse,
  HttpEventType,
  HttpParams,
} from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Subscription } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AskNMateRequest,
  NMateCitation,
  NMateConversationSummary,
  NMateErrorBody,
  NMateMessage,
  NMatePage,
  NMateServerMessage,
  NMateStatus,
  NMateStreamEvent,
} from '../models/nmate.model';
import { SseMessage, SseParser } from '../utils/sse-parser';
import { AuthService } from './auth.service';
import { RepositoryContextService } from './repository-context.service';

const CONVERSATION_KEY = 'nmate.conversationId';

/** First route segments that live directly under `/:orgAlias` rather than under a repository. */
const ORG_LEVEL_SEGMENTS = new Set(['settings', 'my-work', 'profile']);

/** Friendly text per error code returned by NMate or the DASHBOARD proxy. */
const ERROR_TEXT: Record<string, string> = {
  NMATE_QUOTA_EXCEEDED: 'NMate tạm hết lượt sử dụng, bạn thử lại sau ít phút nhé.',
  NMATE_UNAVAILABLE: 'NMate đang bảo trì, bạn thử lại sau nhé.',
  NMATE_STREAM_IN_PROGRESS: 'NMate đang trả lời câu trước của bạn.',
  RATE_LIMITED: 'Bạn hỏi hơi nhanh — đợi khoảng một phút rồi hỏi tiếp nhé.',
  INTERRUPTED: 'Đã dừng.',
};

/**
 * State and API calls for the NMate assistant widget. Answers are streamed through HttpClient
 * (`reportProgress` + `partialText`) so the app's interceptors — token, correlation id, 401 handling —
 * apply exactly as for every other request.
 */
@Injectable({ providedIn: 'root' })
export class NMateService {
  private readonly http    = inject(HttpClient);
  private readonly auth    = inject(AuthService);
  private readonly repoCtx = inject(RepositoryContextService);
  private readonly baseUrl = `${environment.apiBaseUrl}/nmate`;

  /** Whether the panel is open. */
  readonly isOpen = signal(false);
  /** `false` when the backend has NMate switched off (404) — the widget hides entirely. `null` until known. */
  readonly enabled = signal<boolean | null>(null);
  /** Whether NMate can answer right now (model configured, documents indexed). `null` until known. */
  readonly available = signal<boolean | null>(null);
  /** Messages of the current conversation, oldest first. */
  readonly messages = signal<NMateMessage[]>([]);
  /** Starter questions for the current screen. */
  readonly suggestions = signal<string[]>([]);
  /** Current conversation id (kept in sessionStorage so a reload continues the thread). */
  readonly conversationId = signal<string | null>(readConversationId());
  /** True while an answer is being generated. */
  readonly isStreaming = computed(() => this.messages().some(m => m.status === 'streaming'));
  /** The user's most recent conversations, for the history menu. */
  readonly recentConversations = signal<NMateConversationSummary[]>([]);
  /** True while the history menu is loading. */
  readonly historyLoading = signal(false);
  /** True while a picked conversation's messages are loading. */
  readonly conversationLoading = signal(false);

  private streamSub: Subscription | null = null;
  private statusRequested = false;
  private restoreRequested = false;
  /** Guards against a slow response for an earlier pick overwriting a later one. */
  private pickedConversationId: string | null = null;

  /** Opens the panel; loads availability and restores the previous conversation on first open. */
  open(): void {
    this.isOpen.set(true);
    if (!this.statusRequested) this.refreshStatus();
    this.restoreConversation();
  }

  /** Closes the panel. A running answer keeps streaming in the background. */
  close(): void {
    this.isOpen.set(false);
  }

  /** Opens or closes the panel. */
  toggle(): void {
    if (this.isOpen()) this.close();
    else this.open();
  }

  /** Reloads availability from `GET /nmate/status`. A 404 means the feature is switched off. */
  refreshStatus(): void {
    this.statusRequested = true;
    this.http.get<NMateStatus>(`${this.baseUrl}/status`).subscribe({
      next: status => {
        this.enabled.set(true);
        this.available.set(status.available);
      },
      error: (err: HttpErrorResponse) => {
        if (err.status === 404) this.enabled.set(false);
        else this.available.set(false);
      },
    });
  }

  /**
   * Loads starter questions for a screen. Costs no AI quota.
   * @param route Current SPA route.
   */
  loadSuggestions(route: string): void {
    const params = new HttpParams().set('route', route);
    this.http.get<string[]>(`${this.baseUrl}/suggestions`, { params }).subscribe({
      next: suggestions => this.suggestions.set(suggestions),
      error: () => this.suggestions.set([]),
    });
  }

  /**
   * Asks a question and streams the answer into {@link messages}.
   * @param text The question.
   * @param route Route the user is on (boosts that screen's documentation).
   */
  ask(text: string, route: string | null): void {
    const question = text.trim();
    if (!question || this.isStreaming()) return;

    const answerId = newLocalId();
    this.messages.update(list => [
      ...list,
      { localId: newLocalId(), id: null, role: 'user', content: question, citations: [], status: 'done', rating: null },
      { localId: answerId, id: null, role: 'assistant', content: '', citations: [], status: 'streaming', rating: null },
    ]);

    const body: AskNMateRequest = {
      conversationId: this.conversationId(),
      message: question,
      context: { route, repositoryId: this.repoCtx.selectedRepoId() },
    };

    const parser = new SseParser();
    this.streamSub = this.http
      .post(`${this.baseUrl}/chat`, body, { observe: 'events', reportProgress: true, responseType: 'text' })
      .subscribe({
        next: event => {
          if (event.type === HttpEventType.DownloadProgress) {
            this.apply(answerId, parser.feed((event as HttpDownloadProgressEvent).partialText ?? ''));
          } else if (event.type === HttpEventType.Response) {
            this.apply(answerId, parser.feed(event.body ?? ''));
            // Stream ended without done/error (connection cut): keep what arrived.
            this.updateAnswer(answerId, m => (m.status === 'streaming' ? { ...m, status: 'done' } : m));
          }
        },
        error: (err: HttpErrorResponse) => this.fail(answerId, err),
      });
  }

  /** Stops the answer being generated. Unsubscribing aborts the request, which stops generation upstream. */
  stop(): void {
    this.streamSub?.unsubscribe();
    this.streamSub = null;
    this.messages.update(list =>
      list.map(m => (m.status === 'streaming' ? { ...m, status: 'error', errorCode: 'INTERRUPTED' } : m)),
    );
  }

  /** Starts a fresh conversation (the old one stays in history on the server). */
  newConversation(): void {
    this.stop();
    this.pickedConversationId = null;
    this.messages.set([]);
    this.setConversationId(null);
  }

  /**
   * Loads the user's most recent conversations for the history menu (newest activity first).
   * @param take How many to show.
   */
  loadRecentConversations(take = 3): void {
    this.historyLoading.set(true);
    const params = new HttpParams().set('page', 1).set('pageSize', take);
    this.http.get<NMatePage<NMateConversationSummary>>(`${this.baseUrl}/conversations`, { params }).subscribe({
      next: page => {
        this.recentConversations.set(page.items);
        this.historyLoading.set(false);
      },
      error: () => {
        this.recentConversations.set([]);
        this.historyLoading.set(false);
      },
    });
  }

  /**
   * Switches the panel to an earlier conversation so the user can continue it: the next question is
   * sent with this conversation's id. A running answer is stopped first.
   * @param id Conversation to open.
   */
  openConversation(id: string): void {
    if (id === this.conversationId() && this.messages().length > 0) return;

    this.stop();
    this.restoreRequested = true; // a picked thread replaces whatever restore would have loaded
    this.pickedConversationId = id;
    this.conversationLoading.set(true);

    this.http.get<NMateServerMessage[]>(`${this.baseUrl}/conversations/${id}/messages`).subscribe({
      next: stored => {
        if (this.pickedConversationId !== id) return;
        this.setConversationId(id);
        this.messages.set(stored.map(toWidgetMessage));
        this.conversationLoading.set(false);
      },
      error: () => {
        if (this.pickedConversationId !== id) return;
        // Deleted or expired since the list was loaded: drop it from the menu.
        this.recentConversations.update(list => list.filter(c => c.id !== id));
        this.conversationLoading.set(false);
      },
    });
  }

  /**
   * Rates an answer (optimistic; reverted if the call fails).
   * @param message The answer.
   * @param rating 1 helpful, -1 not helpful.
   */
  rate(message: NMateMessage, rating: 1 | -1): void {
    if (!message.id || message.rating === rating) return;
    const previous = message.rating;
    this.updateAnswer(message.localId, m => ({ ...m, rating }));

    this.http.put(`${this.baseUrl}/messages/${message.id}/feedback`, { rating }).subscribe({
      error: () => this.updateAnswer(message.localId, m => ({ ...m, rating: previous })),
    });
  }

  /**
   * Builds the in-app link for a citation. Documents only know the stable part of a route
   * (`/sprint-planning`), so the current org alias and repository code are prefixed here.
   * @param route Citation route.
   * @returns Absolute SPA path, or null when it cannot be resolved.
   */
  citationLink(route: string | null): string | null {
    const org = this.auth.currentUser()?.orgAlias;
    if (!route || !org) return null;

    const path = route.startsWith('/') ? route : `/${route}`;
    if (ORG_LEVEL_SEGMENTS.has(path.split('/')[1])) return `/${org}${path}`;

    const repo = this.repoCtx.selectedRepo()?.code;
    return repo ? `/${org}/${repo}${path}` : null;
  }

  /**
   * Human-readable text for an error code.
   * @param code Error code.
   * @returns Vietnamese message.
   */
  errorText(code: string | undefined): string {
    return (code && ERROR_TEXT[code]) || 'NMate gặp sự cố, bạn thử lại sau nhé.';
  }

  private apply(answerId: string, raw: SseMessage[]): void {
    for (const message of raw) {
      const event = toStreamEvent(message);
      if (!event) continue;

      switch (event.type) {
        case 'meta':
          this.setConversationId(event.conversationId);
          break;
        case 'delta':
          this.updateAnswer(answerId, m => ({ ...m, content: m.content + event.text }));
          break;
        case 'citations':
          this.updateAnswer(answerId, m => ({ ...m, citations: event.citations }));
          break;
        case 'done':
          this.updateAnswer(answerId, m => ({ ...m, id: event.messageId, status: event.answered ? 'done' : 'declined' }));
          break;
        case 'error':
          this.updateAnswer(answerId, m => ({ ...m, id: event.messageId, status: 'error', errorCode: event.code }));
          break;
      }
    }
  }

  private fail(answerId: string, err: HttpErrorResponse): void {
    if (err.status === 404) this.enabled.set(false);

    const code =
      err.status === 429 ? 'RATE_LIMITED'
      : err.status === 0 ? 'NMATE_UNAVAILABLE'
      : (parseError(err.error)?.error ?? 'NMATE_UNAVAILABLE');

    this.updateAnswer(answerId, m => ({ ...m, status: 'error', errorCode: code }));
  }

  private updateAnswer(localId: string, change: (m: NMateMessage) => NMateMessage): void {
    this.messages.update(list => list.map(m => (m.localId === localId ? change(m) : m)));
  }

  private setConversationId(id: string | null): void {
    this.conversationId.set(id);
    try {
      if (id) sessionStorage.setItem(CONVERSATION_KEY, id);
      else sessionStorage.removeItem(CONVERSATION_KEY);
    } catch {
      // Storage unavailable (private mode): the thread just won't survive a reload.
    }
  }

  /** After a reload, brings back the messages of the conversation kept in sessionStorage. */
  private restoreConversation(): void {
    const id = this.conversationId();
    if (this.restoreRequested || !id || this.messages().length > 0) return;
    this.restoreRequested = true;

    this.http.get<NMateServerMessage[]>(`${this.baseUrl}/conversations/${id}/messages`).subscribe({
      next: stored => {
        if (this.messages().length > 0) return; // user already started typing a new thread
        this.messages.set(stored.map(toWidgetMessage));
      },
      // Deleted, expired, or belongs to a previous login in this tab: start fresh.
      error: () => this.setConversationId(null),
    });
  }
}

/** Maps a stored message to the widget's shape (an interrupted answer shows the "stopped" line). */
function toWidgetMessage(m: NMateServerMessage): NMateMessage {
  return {
    localId: newLocalId(),
    id: m.id,
    role: m.role,
    content: m.content,
    citations: m.citations,
    status: m.isInterrupted ? 'error' : 'done',
    errorCode: m.isInterrupted ? 'INTERRUPTED' : undefined,
    rating: m.rating,
  };
}

/** Converts a raw SSE message to a typed event; unknown or malformed events are ignored. */
function toStreamEvent(message: SseMessage): NMateStreamEvent | null {
  try {
    const data = JSON.parse(message.data);
    switch (message.event) {
      case 'meta': return { type: 'meta', conversationId: data.conversationId };
      case 'delta': return { type: 'delta', text: data.text ?? '' };
      case 'citations': return { type: 'citations', citations: data as NMateCitation[] };
      case 'done': return { type: 'done', messageId: data.messageId, latencyMs: data.latencyMs, answered: data.answered };
      case 'error': return { type: 'error', code: data.code, message: data.message, messageId: data.messageId ?? null };
      default: return null;
    }
  } catch {
    return null;
  }
}

/** Error bodies arrive as text (the request used responseType 'text'). */
function parseError(body: unknown): NMateErrorBody | null {
  if (typeof body !== 'string') return (body as NMateErrorBody) ?? null;
  try {
    return JSON.parse(body) as NMateErrorBody;
  } catch {
    return null;
  }
}

function readConversationId(): string | null {
  try {
    return sessionStorage.getItem(CONVERSATION_KEY);
  } catch {
    return null;
  }
}

let localCounter = 0;
function newLocalId(): string {
  return `nm-${Date.now().toString(36)}-${(localCounter++).toString(36)}`;
}
