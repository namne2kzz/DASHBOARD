import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  effect,
  inject,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { filter, map } from 'rxjs';
import { NMateMessage } from '../../models/nmate.model';
import { NMateService } from '../../services/nmate.service';
import { NMateMessageComponent } from '../nmate-message/nmate-message.component';

/**
 * Floating NMate assistant: a button in the bottom-right corner that opens a chat panel.
 * Mounted once in the shell layout. Hidden entirely when the backend has NMate switched off.
 * Shortcut: Ctrl/Cmd + / toggles the panel, Escape closes it.
 */
@Component({
  selector: 'app-nmate-widget',
  imports: [NMateMessageComponent],
  templateUrl: './nmate-widget.component.html',
  styleUrl: './nmate-widget.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NMateWidgetComponent {
  readonly nmate = inject(NMateService);
  private readonly router = inject(Router);

  /** Longest question accepted by NMate. */
  readonly maxLength = 1000;

  /** Text being typed. */
  readonly draft = signal('');

  /** Current SPA route — sent with each question and used to pick suggestions. */
  readonly currentUrl = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map(e => e.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );

  private readonly scroller = viewChild<ElementRef<HTMLElement>>('scroller');
  private readonly input = viewChild<ElementRef<HTMLTextAreaElement>>('questionInput');

  constructor() {
    // Suggestions follow the screen while the panel is open.
    effect(() => {
      if (!this.nmate.isOpen() || this.nmate.enabled() === false) return;
      const url = this.currentUrl();
      untracked(() => this.nmate.loadSuggestions(url));
    });

    // Keep the newest text in view as an answer streams in.
    effect(() => {
      this.nmate.messages();
      const el = this.scroller()?.nativeElement;
      if (el) requestAnimationFrame(() => (el.scrollTop = el.scrollHeight));
    });

    // Put the cursor in the input whenever the panel opens.
    effect(() => {
      if (this.nmate.isOpen()) {
        const el = this.input()?.nativeElement;
        if (el) setTimeout(() => el.focus());
      }
    });
  }

  /**
   * Global shortcuts: Ctrl/Cmd + / toggles the panel; Escape closes it.
   * @param event Keyboard event.
   */
  @HostListener('document:keydown', ['$event'])
  onDocumentKeydown(event: KeyboardEvent): void {
    if ((event.ctrlKey || event.metaKey) && event.key === '/') {
      if (this.nmate.enabled() === false) return;
      event.preventDefault();
      this.nmate.toggle();
    } else if (event.key === 'Escape' && this.nmate.isOpen()) {
      this.nmate.close();
    }
  }

  /**
   * Enter sends, Shift+Enter adds a new line.
   * @param event Keyboard event from the textarea.
   */
  onInputKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) {
      event.preventDefault();
      this.send();
    }
  }

  /**
   * Mirrors the textarea into {@link draft}.
   * @param event Input event.
   */
  onInput(event: Event): void {
    this.draft.set((event.target as HTMLTextAreaElement).value);
  }

  /** Sends the typed question. */
  send(): void {
    const text = this.draft().trim();
    if (!text || this.nmate.isStreaming()) return;
    this.nmate.ask(text, this.currentUrl());
    this.draft.set('');
  }

  /**
   * Sends a suggested question as-is.
   * @param question The suggestion.
   */
  askSuggestion(question: string): void {
    this.nmate.ask(question, this.currentUrl());
  }

  /**
   * Forwards a rating to the service.
   * @param message Rated answer.
   * @param rating 1 or -1.
   */
  onRate(message: NMateMessage, rating: 1 | -1): void {
    this.nmate.rate(message, rating);
  }

  /** Closes the panel after following a source link on narrow screens, where it covers the page. */
  onNavigated(): void {
    if (window.matchMedia('(max-width: 640px)').matches) this.nmate.close();
  }
}
