import { CommonModule } from '@angular/common';
import { AfterViewChecked, Component, ElementRef, HostListener, inject, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { SearchService } from '../../services/search.service';
import { RepositoryContextService } from '../../services/repository-context.service';
import { AuthService } from '../../services/auth.service';
import { SearchResultItem, SearchResultKind } from '../../models/search.model';

/**
 * Global command-palette search overlay. Opened via the sidebar trigger or Ctrl/Cmd+K,
 * it queries the current repository across Work items and Backlog and routes to
 * the relevant section on selection.
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
}

const KIND_ACCENT: Record<SearchResultKind, string> = {
  task:    'text-sky-400 ring-sky-500/35 bg-sky-500/10',
  backlog: 'text-amber-300 ring-amber-400/35 bg-amber-400/10',
};
