import { CommonModule, NgClass } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MyWorkService } from '../../services/my-work.service';
import { RepositoryContextService } from '../../services/repository-context.service';
import { AuthService } from '../../services/auth.service';
import { MyWorkGrouping, MyWorkGroup, MyWorkItemApiDto } from '../../models/my-work.model';
import { SprintTaskApiState, SprintTaskApiType, WorkItemApiPriority } from '../../core/enums/system.enum';
import { SPRINT_TASK_STATE_BADGE, SPRINT_TASK_STATE_LABEL } from '../../core/constants/system.constant';

/**
 * Personal cross-repository work queue: every open work item assigned to the current user,
 * across all repositories they belong to, groupable by sprint, priority, or state.
 */
@Component({
  selector: 'app-my-work-page',
  imports: [CommonModule, NgClass, FormsModule],
  templateUrl: './my-work-page.component.html',
  styleUrl: './my-work-page.component.css',
})
export class MyWorkPageComponent implements OnInit {
  readonly myWork      = inject(MyWorkService);
  private readonly repoCtx = inject(RepositoryContextService);
  private readonly auth    = inject(AuthService);
  private readonly router  = inject(Router);

  readonly stateBadge = SPRINT_TASK_STATE_BADGE;
  readonly stateLabel = SPRINT_TASK_STATE_LABEL;

  // ── Filters ───────────────────────────────────────────────────
  readonly search   = signal('');
  readonly grouping = signal<MyWorkGrouping>('sprint');

  readonly groupingOptions: ReadonlyArray<{ value: MyWorkGrouping; label: string }> = [
    { value: 'sprint',   label: 'Sprint' },
    { value: 'priority', label: 'Priority' },
    { value: 'state',    label: 'State' },
  ];

  private readonly _priorityOrder: WorkItemApiPriority[] = [
    WorkItemApiPriority.Critical, WorkItemApiPriority.High, WorkItemApiPriority.Medium, WorkItemApiPriority.Low,
  ];
  private readonly _stateOrder: SprintTaskApiState[] = [
    SprintTaskApiState.New, SprintTaskApiState.Backlog, SprintTaskApiState.Todo,
    SprintTaskApiState.Active, SprintTaskApiState.InReview,
  ];

  /** Items matching the free-text search (title / work item number / repository). */
  readonly filteredItems = computed(() => {
    const q = this.search().trim().toLowerCase();
    if (!q) return this.myWork.items();
    return this.myWork.items().filter(i =>
      i.title.toLowerCase().includes(q) ||
      i.workItemNumber.toLowerCase().includes(q) ||
      i.repositoryName.toLowerCase().includes(q),
    );
  });

  /** The filtered items bucketed by the active grouping dimension, in a stable display order. */
  readonly groups = computed<MyWorkGroup[]>(() => {
    const items = this.filteredItems();
    switch (this.grouping()) {
      case 'priority': return this._groupByPriority(items);
      case 'state':    return this._groupByState(items);
      default:         return this._groupBySprint(items);
    }
  });

  /** Loads the queue on first render. */
  ngOnInit(): void {
    this.myWork.refresh();
  }

  /** Sets the active grouping dimension. @param grouping The dimension to group by. */
  setGrouping(grouping: MyWorkGrouping): void {
    this.grouping.set(grouping);
  }

  /**
   * Switches the active repository to the item's repository, then navigates to the
   * work-item detail page. Cross-repository items are supported because the repo context
   * is updated before navigation so the page loads the correct data.
   * @param item The work item to open.
   */
  openItem(item: MyWorkItemApiDto): void {
    const orgAlias = this.auth.currentUser()?.orgAlias;
    if (!orgAlias) return;
    this.repoCtx.select(item.repositoryId);
    this.router.navigate(['/', orgAlias, item.repositoryCode, 'boards', item.workItemNumber]);
  }

  // ── Display helpers ───────────────────────────────────────────

  /** @param priority The priority enum value. @returns Human-readable label. */
  priorityLabel(priority: WorkItemApiPriority): string {
    return PRIORITY_LABEL[priority];
  }

  /** @param priority The priority enum value. @returns Tailwind chip classes. */
  priorityBadge(priority: WorkItemApiPriority): string {
    return PRIORITY_BADGE[priority];
  }

  /** @param priority The priority enum value. @returns Tailwind classes for the left accent bar. */
  priorityBar(priority: WorkItemApiPriority): string {
    return PRIORITY_BAR[priority];
  }

  /** @param type The work item type. @returns Short type label. */
  typeLabel(type: SprintTaskApiType): string {
    return TYPE_LABEL[type];
  }

  /** @param type The work item type. @returns Accent ring/text classes for the type chip. */
  typeAccent(type: SprintTaskApiType): string {
    return TYPE_ACCENT[type];
  }

  // ── Grouping ──────────────────────────────────────────────────

  private _groupBySprint(items: MyWorkItemApiDto[]): MyWorkGroup[] {
    const map = new Map<string, MyWorkGroup>();
    for (const item of items) {
      const key = item.sprintId ?? '__none__';
      const label = item.sprintName ?? 'Standalone / No sprint';
      this._push(map, key, label, item);
    }
    return [...map.values()].sort((a, b) => a.label.localeCompare(b.label));
  }

  private _groupByPriority(items: MyWorkItemApiDto[]): MyWorkGroup[] {
    const map = new Map<string, MyWorkGroup>();
    for (const item of items) {
      this._push(map, String(item.priority), this.priorityLabel(item.priority), item);
    }
    return this._priorityOrder
      .map(p => map.get(String(p)))
      .filter((g): g is MyWorkGroup => g !== undefined);
  }

  private _groupByState(items: MyWorkItemApiDto[]): MyWorkGroup[] {
    const map = new Map<string, MyWorkGroup>();
    for (const item of items) {
      this._push(map, String(item.state), this.stateLabel[item.state], item);
    }
    return this._stateOrder
      .map(s => map.get(String(s)))
      .filter((g): g is MyWorkGroup => g !== undefined);
  }

  private _push(map: Map<string, MyWorkGroup>, key: string, label: string, item: MyWorkItemApiDto): void {
    const existing = map.get(key);
    if (existing) existing.items.push(item);
    else map.set(key, { key, label, items: [item] });
  }
}

const PRIORITY_LABEL: Record<WorkItemApiPriority, string> = {
  [WorkItemApiPriority.Low]:      'Low',
  [WorkItemApiPriority.Medium]:   'Medium',
  [WorkItemApiPriority.High]:     'High',
  [WorkItemApiPriority.Critical]: 'Critical',
};

const PRIORITY_BADGE: Record<WorkItemApiPriority, string> = {
  [WorkItemApiPriority.Low]:      'bg-slate-500/10 text-slate-400 ring-slate-400/25',
  [WorkItemApiPriority.Medium]:   'bg-sky-500/10 text-sky-300 ring-sky-500/25',
  [WorkItemApiPriority.High]:     'bg-amber-500/15 text-amber-300 ring-amber-500/30',
  [WorkItemApiPriority.Critical]: 'bg-rose-500/15 text-rose-300 ring-rose-500/35',
};

const PRIORITY_BAR: Record<WorkItemApiPriority, string> = {
  [WorkItemApiPriority.Low]:      'bg-slate-600/60',
  [WorkItemApiPriority.Medium]:   'bg-sky-500/70',
  [WorkItemApiPriority.High]:     'bg-amber-500/80',
  [WorkItemApiPriority.Critical]: 'bg-rose-500',
};

const TYPE_LABEL: Record<SprintTaskApiType, string> = {
  [SprintTaskApiType.UserStory]: 'User Story',
  [SprintTaskApiType.Task]:      'Task',
  [SprintTaskApiType.Bug]:       'Bug',
  [SprintTaskApiType.TestPlan]:  'Test Plan',
};

const TYPE_ACCENT: Record<SprintTaskApiType, string> = {
  [SprintTaskApiType.UserStory]: 'text-blue-400 ring-blue-500/35 bg-blue-500/10',
  [SprintTaskApiType.Task]:      'text-amber-300 ring-amber-400/35 bg-amber-400/10',
  [SprintTaskApiType.Bug]:       'text-rose-400 ring-rose-500/35 bg-rose-500/10',
  [SprintTaskApiType.TestPlan]:  'text-violet-400 ring-violet-500/35 bg-violet-500/10',
};
