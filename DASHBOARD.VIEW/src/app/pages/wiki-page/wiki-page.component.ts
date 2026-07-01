import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';
import { WikiEditorComponent } from '../../components/wiki-editor/wiki-editor.component';
import { WikiPage } from '../../models/wiki.model';
import { ResourceService } from '../../services/resource.service';
import { WikiService } from '../../services/wiki.service';
import { ensureWikiHtml } from '../../utils/wiki-content.util';

export interface WikiTreeRow {
  page: WikiPage;
  depth: number;
}

@Component({
  selector: 'app-wiki-page',
  standalone: true,
  imports: [CommonModule, FormsModule, DatePipe, WikiEditorComponent],
  templateUrl: './wiki-page.component.html',
  styleUrl: './wiki-page.component.css',
})
export class WikiPageComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly sanitizer = inject(DomSanitizer);
  readonly wiki = inject(WikiService);
  readonly resource = inject(ResourceService);
  readonly t = this.resource.get.bind(this.resource);

  readonly isEditing = signal(false);
  readonly copyFeedback = signal<string | null>(null);

  draftTitle = '';
  draftContent = '';

  private readonly routePageId = toSignal(
    this.route.paramMap.pipe(map((p) => p.get('pageId')?.toUpperCase() ?? null)),
    { initialValue: null as string | null },
  );

  readonly treeRows = computed<WikiTreeRow[]>(() => {
    const rows: WikiTreeRow[] = [];
    const walk = (pages: WikiPage[], depth: number) => {
      for (const page of pages) {
        rows.push({ page, depth });
        walk(this.wiki.childrenOf(page.id), depth + 1);
      }
    };
    walk(this.wiki.rootPages(), 0);
    return rows;
  });

  readonly renderedHtml = computed<SafeHtml | null>(() => {
    const page = this.wiki.selectedPage();
    if (!page) {
      return null;
    }
    return this.sanitizer.bypassSecurityTrustHtml(ensureWikiHtml(page.content));
  });

  constructor() {
    effect(() => {
      const id = this.routePageId();
      if (id && this.wiki.getPage(id)) {
        this.wiki.selectPageId(id);
        this.isEditing.set(false);
      }
    });

    effect(() => {
      const page = this.wiki.selectedPage();
      if (page && !this.isEditing()) {
        this.draftTitle = page.title;
        this.draftContent = ensureWikiHtml(page.content);
      }
    });
  }

  selectPage(page: WikiPage): void {
    this.isEditing.set(false);
    this.wiki.selectPageId(page.id);
    void this.router.navigate(['/wiki', page.id]);
  }

  startEdit(): void {
    if (!this.wiki.canManageWiki()) return;
    const page = this.wiki.selectedPage();
    if (!page) {
      return;
    }
    this.draftTitle = page.title;
    this.draftContent = ensureWikiHtml(page.content);
    this.isEditing.set(true);
  }

  cancelEdit(): void {
    const page = this.wiki.selectedPage();
    if (page) {
      this.draftTitle = page.title;
      this.draftContent = ensureWikiHtml(page.content);
    }
    this.isEditing.set(false);
  }

  save(): void {
    if (!this.wiki.canManageWiki()) return;
    const page = this.wiki.selectedPage();
    if (!page) {
      return;
    }
    this.wiki.upsertPage({
      ...page,
      title: this.draftTitle,
      content: this.draftContent,
    });
    this.isEditing.set(false);
  }

  async copyLink(): Promise<void> {
    const page = this.wiki.selectedPage();
    if (!page) {
      return;
    }
    const ok = await this.wiki.copyPageLink(page.id);
    this.copyFeedback.set(
      ok ? this.resource.get('wiki.copy.success') : this.resource.get('wiki.copy.failure'),
    );
    setTimeout(() => this.copyFeedback.set(null), 2000);
  }

  addPage(): void {
    const created = this.wiki.createPage('New design page');
    if (!created) return;
    this.selectPage(created);
    this.startEdit();
  }
}
