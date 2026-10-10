import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NMateCitation, NMateMessage } from '../../models/nmate.model';
import { NMateService } from '../../services/nmate.service';
import { renderMarkdown } from '../../utils/markdown.util';

/** One NMate chat bubble: the user's question, or an answer with its sources and a 👍/👎 control. */
@Component({
  selector: 'app-nmate-message',
  imports: [RouterLink],
  templateUrl: './nmate-message.component.html',
  styleUrl: './nmate-message.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NMateMessageComponent {
  private readonly nmate = inject(NMateService);

  /** The message to show. */
  readonly message = input.required<NMateMessage>();
  /** Emitted when the user rates the answer. */
  readonly rate = output<1 | -1>();
  /** Emitted when a source link is followed (the widget closes on small screens). */
  readonly navigated = output<void>();

  /** Rendered markdown of an answer; Angular's [innerHTML] sanitizer strips anything unsafe. */
  readonly html = computed(() => {
    const m = this.message();
    return m.role === 'assistant' && m.content.trim() ? renderMarkdown(m.content) : '';
  });

  /** Whether the 👍/👎 control applies (finished answers the server knows about). */
  readonly canRate = computed(() => {
    const m = this.message();
    return m.role === 'assistant' && m.id !== null && (m.status === 'done' || m.status === 'declined');
  });

  /** Friendly error line for a failed or stopped answer, or null. */
  readonly errorText = computed(() => {
    const m = this.message();
    return m.status === 'error' ? this.nmate.errorText(m.errorCode) : null;
  });

  /**
   * In-app link for a source.
   * @param citation The source.
   * @returns SPA path, or null when it cannot be resolved here.
   */
  linkFor(citation: NMateCitation): string | null {
    return this.nmate.citationLink(citation.route);
  }

  /** Distinct sources — several chunks of one section collapse into a single chip. */
  readonly sources = computed<NMateCitation[]>(() => {
    const seen = new Set<string>();
    return this.message().citations.filter(c => {
      const key = `${c.title}›${c.headingPath}`;
      if (seen.has(key)) return false;
      seen.add(key);
      return true;
    });
  });
}
