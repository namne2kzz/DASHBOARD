import { computed, inject, Injectable, signal } from '@angular/core';
import { WikiPage } from '../models/wiki.model';
import { ensureWikiHtml } from '../utils/wiki-content.util';
import { buildWikiPageUrl } from '../utils/wiki-link.util';
import { StorageKeys } from '../core/constants/storage-keys.constant';
import { PrivilegeService } from '../core/services/privilege.service';

function nowIso(): string {
  return new Date().toISOString();
}

function nextWikiId(existing: WikiPage[]): string {
  const nums = existing
    .map((p) => {
      const m = /^W-(\d+)$/i.exec(p.id);
      return m ? parseInt(m[1], 10) : 0;
    })
    .filter((n) => !Number.isNaN(n));
  const max = nums.length ? Math.max(...nums) : 100;
  return `W-${max + 1}`;
}

const SEED: WikiPage[] = [
  {
    id: 'W-101',
    title: 'System Architecture Overview',
    parentId: null,
    lastUpdated: '2026-05-10T09:00:00Z',
    content: `# System Architecture

High-level design for the ALM dashboard clone.

## Components

- **Shell** — sidebar navigation + router outlet
- **Boards** — Kanban with CDK drag-drop
- **Wiki** — design docs (this page)

\`\`\`text
[Browser] → Angular SPA → localStorage
\`\`\`

## Related work items

Link design pages from bugs and user stories using **Wiki Link** in the work item panel.
`,
  },
  {
    id: 'W-102',
    title: 'Kanban Board — Data Flow',
    parentId: 'W-101',
    lastUpdated: '2026-05-12T14:30:00Z',
    content: `# Kanban data flow

1. \`TaskBoardService\` holds work items in a **signal**
2. \`filteredTasks\` is a **computed** signal
3. Drag-drop calls \`persistFromColumns\` and writes **audit history**

> Status changes are logged automatically when cards move between columns.
`,
  },
  {
    id: 'W-103',
    title: 'Authentication (Future)',
    parentId: 'W-101',
    lastUpdated: '2026-05-08T11:00:00Z',
    content: `# Authentication — placeholder

Backend not implemented. Mock users drive **Assigned To** and discussion authors.

When integrating Entra ID / OAuth, replace \`UsersMockService\` with a real profile service.
`,
  },
];

@Injectable({ providedIn: 'root' })
export class WikiService {
  private readonly privilege = inject(PrivilegeService);
  private readonly pagesSignal = signal<WikiPage[]>([]);

  /** True when the current user can create, edit, and delete wiki pages. */
  readonly canManageWiki = computed(() => this.privilege.canManageWiki());

  readonly pages = this.pagesSignal.asReadonly();

  readonly searchQuery = signal('');

  readonly filteredPages = computed(() => {
    const q = this.searchQuery().trim().toLowerCase();
    const list = this.pagesSignal();
    if (!q) {
      return list;
    }
    return list.filter(
      (p) =>
        p.title.toLowerCase().includes(q) ||
        p.id.toLowerCase().includes(q) ||
        p.content.toLowerCase().includes(q),
    );
  });

  /** Tree roots (no parent or parent missing) */
  readonly rootPages = computed(() => {
    const all = this.filteredPages();
    const ids = new Set(all.map((p) => p.id));
    return all.filter((p) => !p.parentId || !ids.has(p.parentId));
  });

  constructor() {
    this.load();
  }

  getPage(id: string): WikiPage | undefined {
    return this.pagesSignal().find((p) => p.id.toUpperCase() === id.toUpperCase());
  }

  childrenOf(parentId: string): WikiPage[] {
    return this.filteredPages().filter(
      (p) => p.parentId?.toUpperCase() === parentId.toUpperCase(),
    );
  }

  private readonly selectedPageIdSignal = signal<string | null>(null);

  readonly selectedPageId = this.selectedPageIdSignal.asReadonly();

  selectPageId(pageId: string | null): void {
    this.selectedPageIdSignal.set(pageId);
  }

  readonly selectedPage = computed(() => {
    const id = this.selectedPageIdSignal();
    return id ? this.getPage(id) : undefined;
  });

  pageUrl(pageId: string): string {
    return buildWikiPageUrl(pageId);
  }

  async copyPageLink(pageId: string): Promise<boolean> {
    const url = this.pageUrl(pageId);
    try {
      await navigator.clipboard.writeText(url);
      return true;
    } catch {
      return false;
    }
  }

  upsertPage(page: WikiPage): void {
    const normalized: WikiPage = {
      ...page,
      id: page.id.toUpperCase(),
      lastUpdated: nowIso(),
    };
    this.pagesSignal.update((list) => {
      const idx = list.findIndex((p) => p.id.toUpperCase() === normalized.id.toUpperCase());
      if (idx === -1) {
        return [...list, normalized];
      }
      const copy = [...list];
      copy[idx] = normalized;
      return copy;
    });
    this.persist();
  }

  createPage(title: string, parentId?: string | null): WikiPage {
    const page: WikiPage = {
      id: nextWikiId(this.pagesSignal()),
      title: title.trim() || 'Untitled design page',
      content: `<h1>${title.trim() || 'Untitled'}</h1><p>Write design notes here.</p>`,
      lastUpdated: nowIso(),
      parentId: parentId ?? null,
    };
    this.pagesSignal.update((list) => [...list, page]);
    this.persist();
    return page;
  }

  deletePage(id: string): void {
    const upper = id.toUpperCase();
    this.pagesSignal.update((list) =>
      list
        .filter((p) => p.id.toUpperCase() !== upper)
        .map((p) => (p.parentId?.toUpperCase() === upper ? { ...p, parentId: null } : p)),
    );
    if (this.selectedPageIdSignal()?.toUpperCase() === upper) {
      this.selectedPageIdSignal.set(null);
    }
    this.persist();
  }

  private load(): void {
    const raw = localStorage.getItem(StorageKeys.wikiPages);
    if (!raw) {
      this.pagesSignal.set(SEED);
      localStorage.setItem(StorageKeys.wikiPages, JSON.stringify(SEED));
      return;
    }
    try {
      const parsed = JSON.parse(raw) as WikiPage[];
      if (!Array.isArray(parsed) || parsed.length === 0) {
        this.pagesSignal.set(SEED);
        localStorage.setItem(StorageKeys.wikiPages, JSON.stringify(SEED));
      } else {
        this.applyPages(parsed);
      }
    } catch {
      this.pagesSignal.set(SEED);
      localStorage.setItem(StorageKeys.wikiPages, JSON.stringify(SEED));
    }
  }

  private applyPages(pages: WikiPage[]): void {
    let changed = false;
    const normalized = pages.map((p) => {
      const html = ensureWikiHtml(p.content);
      if (html !== p.content) {
        changed = true;
        return { ...p, content: html };
      }
      return p;
    });
    this.pagesSignal.set(normalized);
    if (changed) {
      this.persist();
    }
  }

  private persist(): void {
    localStorage.setItem(StorageKeys.wikiPages, JSON.stringify(this.pagesSignal()));
  }
}
