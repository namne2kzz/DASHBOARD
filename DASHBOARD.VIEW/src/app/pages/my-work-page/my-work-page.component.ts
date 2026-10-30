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
import { LoadErrorComponent } from '../../components/load-error/load-error.component';
import { NBotComponent } from '../../components/n-bot/n-bot.component';

/** The priority bar down a row's left edge, by priority. */
const PRIORITY_COLORS: Record<WorkItemApiPriority, string> = {
  [WorkItemApiPriority.Critical]: 'var(--danger)',
  [WorkItemApiPriority.High]:     'var(--danger)',
  [WorkItemApiPriority.Medium]:   'var(--warning)',
  [WorkItemApiPriority.Low]:      'var(--border-strong)',
};

/**
 * Personal cross-repository work queue: every open work item assigned to the current user,
 * across all repositories they belong to, groupable by sprint, priority, or state.
 */
@Component({
  selector: 'app-my-work-page',
  imports: [LoadErrorComponent, CommonModule, NgClass, FormsModule, NBotComponent],
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

  // ── Greeting ──────────────────────────────────────────────────

  /** Today, as "Monday, 27 October". */
  readonly todayLabel = computed(() =>
    new Date().toLocaleDateString('en-GB', { weekday: 'long', day: 'numeric', month: 'long' }).toUpperCase(),
  );

  /**
   * "Good morning, Nam" — time of day plus the first word of the person's name.
   *
   * The first word, not the last: display names here are free text and frequently carry a
   * trailing nickname or note, which a greeting should not read back to them. The boundaries
   * are the ordinary ones rather than anything clever; a greeting that is occasionally a
   * little early is better than one that reads as a system message.
   */
  readonly greeting = computed(() => {
    const hour = new Date().getHours();
    const part = hour < 12 ? 'Good morning' : hour < 18 ? 'Good afternoon' : 'Good evening';
    const [first] = (this.auth.currentUser()?.name ?? '').trim().split(/\s+/).filter(Boolean);
    return first ? `${part}, ${first}` : part;
  });

  /**
   * The day's workload in a sentence.
   *
   * Written out rather than left as four counters because the shape of the day — whether
   * anything is urgent, whether anyone is waiting on a review — is the thing worth knowing
   * first, and reading it off tiles takes longer than reading it.
   */
  readonly summary = computed(() => {
    const total    = this.myWork.totalCount();
    const inReview = this.myWork.inReviewCount();
    if (total === 0) return 'Nothing is assigned to you right now.';

    const urgent = this.myWork.items().filter(
      i => i.priority === WorkItemApiPriority.Critical || i.priority === WorkItemApiPriority.High,
    ).length;

    const openPart = `You have ${total} open item${total === 1 ? '' : 's'}`;
    const urgentPart = urgent > 0 ? `, ${urgent} of them high priority` : '';
    const reviewPart = inReview > 0
      ? ` ${inReview} ${inReview === 1 ? 'is' : 'are'} waiting on review.`
      : ' Nothing is waiting on review.';

    return `${openPart}${urgentPart}.${reviewPart}`;
  });

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
    SprintTaskApiState.Open, SprintTaskApiState.ToDo,
    SprintTaskApiState.InProgress, SprintTaskApiState.InReview,
    SprintTaskApiState.Verified, SprintTaskApiState.Running,
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

  /**
   * Colour of the bar down a row's left edge.
   *
   * It repeats what the priority chip says, which is deliberate: the chip is hidden on narrow
   * screens, and the bar stays readable while scrolling a long list where chips do not.
   *
   * @param priority The item's priority.
   * @returns A CSS colour token reference.
   */
  priorityColor(priority: WorkItemApiPriority): string {
    return PRIORITY_COLORS[priority];
  }


  /** @param type The work item type. @returns Short type label. */
  typeLabel(type: SprintTaskApiType): string {
    return TYPE_LABEL[type];
  }

  /** @param type The work item type. @returns Class for the 8px type square. */
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
  [WorkItemApiPriority.Low]:      'bg-slate-800 text-slate-400',
  [WorkItemApiPriority.Medium]:   'bg-slate-800 text-slate-400',
  [WorkItemApiPriority.High]:     'bg-amber-500/10 text-amber-500 ring-transparent',
  [WorkItemApiPriority.Critical]: 'bg-rose-500/10 text-rose-500 ring-transparent',
};


const TYPE_LABEL: Record<SprintTaskApiType, string> = {
  [SprintTaskApiType.UserStory]: 'User Story',
  [SprintTaskApiType.Task]:      'Task',
  [SprintTaskApiType.Bug]:       'Bug',
  [SprintTaskApiType.TestPlan]:  'Test Plan',
};

const TYPE_ACCENT: Record<SprintTaskApiType, string> = {
  [SprintTaskApiType.UserStory]: 'nx-type--story',
  [SprintTaskApiType.Task]:      'nx-type--task',
  [SprintTaskApiType.Bug]:       'nx-type--bug',
  [SprintTaskApiType.TestPlan]:  'nx-type--test',
};
