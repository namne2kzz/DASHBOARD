import { CommonModule } from '@angular/common';
import { AfterViewChecked, Component, ElementRef, HostListener, computed, inject, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { SearchService } from '../../services/search.service';
import { RepositoryContextService } from '../../services/repository-context.service';
import { AuthService } from '../../services/auth.service';
import { ThemeService } from '../../core/services/theme.service';
import { NMateService } from '../../services/nmate.service';
import { SearchResultItem, SearchResultKind } from '../../models/search.model';
import { PaletteCommand } from '../../models/command.model';

/**
 * Global command palette. Opened via the sidebar trigger or Ctrl/Cmd+K, it searches the
 * current repository across Work items and Backlog, and runs commands — creating a sprint,
 * switching theme, handing the typed text to NMate — so the same keystroke that finds things
 * also does them.
 */
@Component({
  selector: 'app-global-search',
  imports: [CommonModule, FormsModule],
  templateUrl: './global-search.component.html',
  styleUrl: './global-search.component.css',
})
export class GlobalSearchComponent implements AfterViewChecked {
  readonly search  = inject(SearchService);
  private readonly repoCtx = inject(RepositoryContextService);
  private readonly router  = inject(Router);
  private readonly auth    = inject(AuthService);
  private readonly theme   = inject(ThemeService);
  private readonly nmate   = inject(NMateService);

  /** Everything the palette can do, before filtering by what has been typed. */
  private readonly allCommands = computed<PaletteCommand[]>(() => [
    {
      id: 'new-work-item',
      label: 'New work item',
      keywords: ['create', 'add', 'task', 'story', 'bug'],
      hint: 'C',
      icon: 'M12 5v14M5 12h14',
      run: () => this.goToRepoPage('boards', { new: 'work-item' }),
    },
    {
      id: 'new-sprint',
      label: 'New sprint',
      keywords: ['create', 'add', 'iteration'],
      icon: 'M3 4h18v18H3zM16 2v4M8 2v4M3 10h18',
      run: () => this.goToRepoPage('sprint-planning', { new: 'sprint' }),
    },
    {
      id: 'toggle-theme',
      label: this.theme.theme() === 'dark' ? 'Switch to light mode' : 'Switch to dark mode',
      keywords: ['theme', 'appearance', 'dark', 'light'],
      icon: 'M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9z',
      run: () => this.theme.toggle(),
    },
    {
      id: 'go-backlog',
      label: 'Go to Backlog',
      keywords: ['navigate', 'open'],
      icon: 'M8 6h13M8 12h13M8 18h13M3 6h.01M3 12h.01M3 18h.01',
      run: () => this.goToRepoPage('backlog'),
    },
    {
      id: 'go-board',
      label: 'Go to Board',
      keywords: ['navigate', 'open', 'kanban'],
      icon: 'M3 3h5v18H3zM10 3h5v12h-5zM17 3h4v15h-4z',
      run: () => this.goToRepoPage('boards'),
    },
  ]);

  /**
   * Commands matching what has been typed, by label or by the words people actually reach for.
   *
   * With an empty box the list is shown in full, which is how someone discovers that the
   * palette does anything beyond search.
   */
  readonly commands = computed(() => {
    const term = this.search.term().trim().toLowerCase();
    if (!term) return this.allCommands();

    return this.allCommands().filter(c =>
      c.label.toLowerCase().includes(term) || (c.keywords ?? []).some(k => k.includes(term)),
    );
  });

  /** True once there is enough text to be worth handing to NMate as a question. */
  readonly canAskNMate = computed(() => this.search.term().trim().length >= 2);

  private readonly input = viewChild<ElementRef<HTMLInputElement>>('searchInput');
  private _focused = false;

  /** Global shortcut: Ctrl/Cmd+K toggles the palette; Escape closes it. */
  @HostListener('document:keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
      event.preventDefault();
      if (this.search.open()) this.search.close();
      else if (this.repoCtx.selectedRepoId()) this.search.openPalette();
      return;
    }
    if (event.key === 'Escape' && this.search.open()) {
      this.search.close();
    }
  }

  /** Focuses the input the first time the palette becomes visible. */
  ngAfterViewChecked(): void {
    if (this.search.open() && !this._focused) {
      this.input()?.nativeElement.focus();
      this._focused = true;
    } else if (!this.search.open()) {
      this._focused = false;
    }
  }

  /** @param icon The kind to label. @returns Tailwind accent classes for the kind badge. */
  kindAccent(kind: SearchResultKind): string {
    return KIND_ACCENT[kind];
  }

  /**
   * Navigates to the selected hit: sprint-task hits open the work-item detail page;
   * Backlog hits navigate to the backlog section.
   * @param item The selected search hit.
   */
  goTo(item: SearchResultItem): void {
    this.search.close();

    const code  = this.repoCtx.selectedRepo()?.code;
    const alias = this.auth.currentUser()?.orgAlias ?? '';
    if (!code) return;

    if (item.kind === 'task') {
      // subtitle format: "DASH-34 · UserStory" — extract the work-item key for the URL.
      const itemKey = item.subtitle?.split(' ·')[0]?.trim();
      if (itemKey) {
        void this.router.navigate(['/', alias, code, 'boards', itemKey]);
      }
      return;
    }

    void this.router.navigate(['/', alias, code, 'backlog']);
  }

  /**
   * Closes the palette and runs the command.
   *
   * @param command The command to run.
   */
  runCommand(command: PaletteCommand): void {
    this.search.close();
    command.run();
  }

  /**
   * Hands the typed text to NMate as a question.
   *
   * The last resort of a palette: when nothing matched, what was typed was probably a
   * question rather than a bad search term.
   */
  askNMate(): void {
    const question = this.search.term().trim();
    this.search.close();
    this.nmate.open();
    this.nmate.ask(question, this.router.url);
  }

  /**
   * Navigates to a page in the current repository.
   *
   * @param page Sub-route, e.g. 'boards'.
   * @param queryParams Optional query parameters, used to open a dialog on arrival.
   */
  private goToRepoPage(page: string, queryParams?: Record<string, string>): void {
    const code  = this.repoCtx.selectedRepo()?.code;
    const alias = this.auth.currentUser()?.orgAlias ?? '';
    if (!code) return;

    void this.router.navigate(['/', alias, code, page], queryParams ? { queryParams } : undefined);
  }
}

/** The 10px type square that leads each result row. */
const KIND_ACCENT: Record<SearchResultKind, string> = {
  task:    'nx-type--story',
  backlog: 'nx-type--task',
};
