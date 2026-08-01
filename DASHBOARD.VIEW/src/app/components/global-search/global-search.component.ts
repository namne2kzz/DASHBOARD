import { CommonModule } from '@angular/common';
import { AfterViewChecked, Component, ElementRef, HostListener, inject, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { SearchService } from '../../services/search.service';
import { RepositoryContextService } from '../../services/repository-context.service';
import { DialogService } from '../../core/components/dialog/dialog.service';
import { SprintTaskDetailDialogComponent } from '../sprint-task-detail-dialog/sprint-task-detail-dialog.component';
import { SearchResultItem, SearchResultKind } from '../../models/search.model';

/**
 * Global command-palette search overlay. Opened via the sidebar trigger or Ctrl/Cmd+K,
 * it queries the current repository across Work items, Backlog, and Wiki and routes to
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
  private readonly dialog  = inject(DialogService);

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
   * Opens the selected hit: work items open their detail dialog in place; Backlog and Wiki hits
   * navigate to their section (the exact item may not be visible on the board).
   * @param item The selected search hit.
   */
  goTo(item: SearchResultItem): void {
    this.search.close();

    if (item.kind === 'task') {
      this.dialog.open(SprintTaskDetailDialogComponent, {
        title: item.subtitle?.split(' ·')[0] ?? item.title,
        width: '44rem',
        data:  { taskId: item.id },
      });
      return;
    }

    const code = this.repoCtx.selectedRepo()?.code;
    if (!code) return;
    void this.router.navigate(['/', code, item.kind === 'wiki' ? 'wiki' : 'backlog']);
  }
}

const KIND_ACCENT: Record<SearchResultKind, string> = {
  task:    'text-sky-400 ring-sky-500/35 bg-sky-500/10',
  backlog: 'text-amber-300 ring-amber-400/35 bg-amber-400/10',
  wiki:    'text-violet-400 ring-violet-500/35 bg-violet-500/10',
};
