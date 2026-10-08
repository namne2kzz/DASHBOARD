import { CommonModule, NgClass } from '@angular/common';
import { Component, computed, HostListener, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuditLogService } from '../../services/audit-log.service';
import { MembersService } from '../../services/members.service';
import { InfiniteScrollDirective } from '../../directives/infinite-scroll.directive';
import { FlipDropDirective } from '../../directives/flip-drop.directive';
import { AuditLogCategory, AuditLogFilters } from '../../models/audit-log.model';

/**
 * Repository-wide audit log for admins: a filterable, paged view of every recorded change,
 * filterable by author, date range, and change category.
 */
@Component({
  selector: 'app-audit-log-page',
  imports: [CommonModule, NgClass, FormsModule, InfiniteScrollDirective, FlipDropDirective],
  templateUrl: './audit-log-page.component.html',
  styleUrl: './audit-log-page.component.css',
})
export class AuditLogPageComponent implements OnInit {
  readonly audit   = inject(AuditLogService);
  readonly members = inject(MembersService);

  // ── Filters ───────────────────────────────────────────────────
  readonly authorId          = signal('');
  readonly from              = signal('');
  readonly to                = signal('');
  readonly category          = signal<AuditLogCategory | ''>('');
  readonly showAuthorPicker  = signal(false);

  @HostListener('document:click')
  onDocumentClick(): void { this.showAuthorPicker.set(false); }

  readonly categoryOptions: ReadonlyArray<{ value: AuditLogCategory | ''; label: string }> = [
    { value: '',           label: 'All actions' },
    { value: 'created',    label: 'Created' },
    { value: 'state',      label: 'State change' },
    { value: 'assignment', label: 'Assignment' },
    { value: 'update',     label: 'Field update' },
  ];

  /** True when any filter is active. */
  readonly hasActiveFilters = computed(() =>
    !!this.authorId() || !!this.from() || !!this.to() || !!this.category());

  /** Loads the first page on entry. */
  ngOnInit(): void {
    this.audit.refresh();
  }

  /** Pushes the current filter selection to the service and reloads. */
  applyFilters(): void {
    const filters: AuditLogFilters = {
      authorId: this.authorId(),
      from:     this.from(),
      to:       this.to(),
      category: this.category(),
    };
    this.audit.applyFilters(filters);
  }

  /** Clears all filters and reloads. */
  clearFilters(): void {
    this.authorId.set('');
    this.from.set('');
    this.to.set('');
    this.category.set('');
    this.applyFilters();
  }

  /** @param category The entry category. @returns Tailwind chip classes. */
  categoryBadge(category: AuditLogCategory): string {
    return CATEGORY_BADGE[category];
  }

  /** @param category The entry category. @returns Human-readable label. */
  categoryLabel(category: AuditLogCategory): string {
    return CATEGORY_LABEL[category];
  }
}

const CATEGORY_LABEL: Record<AuditLogCategory, string> = {
  created:    'Created',
  state:      'State',
  assignment: 'Assignment',
  update:     'Update',
};

const CATEGORY_BADGE: Record<AuditLogCategory, string> = {
  created:    'bg-emerald-500/12 text-emerald-300 ring-transparent',
  state:      'bg-sky-500/10 text-sky-500 ring-transparent',
  assignment: 'bg-violet-500/10 text-violet-500 ring-transparent',
  update:     'bg-slate-500/12 text-slate-400 ring-slate-400/25',
};
