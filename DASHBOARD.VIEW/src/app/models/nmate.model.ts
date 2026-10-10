/** Who wrote a chat message. */
export type NMateRole = 'user' | 'assistant';

/** Lifecycle of an answer in the widget. */
export type NMateMessageStatus = 'streaming' | 'done' | 'declined' | 'error';

/** A documentation section an answer was grounded on. `route` is the stable part only, e.g. `/sprint-planning`. */
export interface NMateCitation {
  chunkId: string;
  title: string;
  headingPath: string;
  route: string | null;
  score: number;
}

/** One message as shown in the widget. */
export interface NMateMessage {
  /** Client-side key for `@for` tracking (server ids arrive only at the end of a stream). */
  localId: string;
  /** Server id — set for answers once `done` arrives, needed to submit feedback. */
  id: string | null;
  role: NMateRole;
  content: string;
  citations: NMateCitation[];
  status: NMateMessageStatus;
  rating: 1 | -1 | null;
  /** Error code when `status === 'error'` (e.g. `NMATE_QUOTA_EXCEEDED`). */
  errorCode?: string;
}

/** Body of `POST /nmate/chat`. */
export interface AskNMateRequest {
  conversationId: string | null;
  message: string;
  context: { route: string | null; repositoryId: string | null };
}

/** Server-sent events of a streamed answer, in order: meta → delta… → citations → done (or error). */
export type NMateStreamEvent =
  | { type: 'meta'; conversationId: string }
  | { type: 'delta'; text: string }
  | { type: 'citations'; citations: NMateCitation[] }
  | { type: 'done'; messageId: string; latencyMs: number; answered: boolean }
  | { type: 'error'; code: string; message: string; messageId: string | null };

/** Availability reported by `GET /nmate/status`. */
export interface NMateStatus {
  available: boolean;
  chatModel: string;
  activeDocuments: number;
  lastIngestionAt: string | null;
}

/** A stored message as returned by `GET /nmate/conversations/{id}/messages`. */
export interface NMateServerMessage {
  id: string;
  role: NMateRole;
  content: string;
  citations: NMateCitation[];
  isInterrupted: boolean;
  rating: 1 | -1 | null;
  createdAt: string;
}

/** Error body returned by NMate / the DASHBOARD proxy. */
export interface NMateErrorBody {
  error?: string;
  message?: string;
}
