import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Component, computed, effect, inject, untracked } from '@angular/core';
import { Color, NgxChartsModule, ScaleType } from '@swimlane/ngx-charts';
import { ResourceService } from '../../services/resource.service';
import { OverviewService } from '../../services/overview.service';
import { timeAgo } from '../../utils/time-ago.util';
import { LoadErrorComponent } from '../../components/load-error/load-error.component';
import { MetricDeltaComponent } from '../../components/metric-delta/metric-delta.component';
import { RepositoryContextService } from '../../services/repository-context.service';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { NMateNudgeService } from '../../services/nmate-nudge.service';

const TYPE_LABELS: Record<string, string> = {
  UserStory: 'User Story',
  Task: 'Task',
  Bug: 'Bug',
  TestPlan: 'Test Plan',
};

// Chart palettes: ngx-charts writes fills as SVG attributes (no CSS vars), so these are
// mid-tone values of the Nexus UI v2 type/state colours that read on both themes.
const TYPE_SCHEME: Color = {
  name: 'type',
  selectable: true,
  group: ScaleType.Ordinal,
  domain: ['#3B6FD4', '#B7791F', '#CF222E', '#7550C9'], // type-story · type-task · type-bug · type-test
};

const STATUS_SCHEME: Color = {
  name: 'status',
  selectable: true,
  group: ScaleType.Ordinal,
  domain: ['#8B949E', '#3B6FD4', '#BF8700', '#1A7F37'], // New · Active · In Review · Done
};

const VELOCITY_SCHEME: Color = {
  name: 'velocity',
  selectable: true,
  group: ScaleType.Ordinal,
  domain: ['#8B949E', '#7C3AED'], // committed (neutral) · completed (accent)
};

const BURNDOWN_SCHEME: Color = {
  name: 'burndown',
  selectable: true,
  group: ScaleType.Ordinal,
  domain: ['#8B949E', '#7C3AED'], // ideal (neutral, drawn dashed) · actual (accent)
};

const WORKLOAD_SCHEME: Color = {
  name: 'workload',
  selectable: true,
  group: ScaleType.Ordinal,
  domain: ['#7C3AED'],
};

const HEALTH_BADGES: Record<string, { label: string; classes: string }> = {
  OnTrack: { label: 'On track', classes: 'bg-nx-success-soft text-nx-success ring-transparent' },
  AtRisk:  { label: 'At risk',  classes: 'bg-nx-warning-soft text-nx-warning ring-transparent' },
  Behind:  { label: 'Behind',   classes: 'bg-nx-danger-soft text-nx-danger ring-transparent' },
};

/** The stripe colour along the top of the current-sprint card, by health. */
const HEALTH_STRIPES: Record<string, string> = {
  OnTrack: 'var(--success)',
  AtRisk:  'var(--warning)',
  Behind:  'var(--danger)',
};

@Component({
  selector: 'app-overview-page',
  imports: [LoadErrorComponent, CommonModule, FormsModule, NgxChartsModule, MetricDeltaComponent, RouterLink],
  templateUrl: './overview-page.component.html',
  styleUrl: './overview-page.component.css',
})
export class OverviewPageComponent {
  private readonly overviewSvc = inject(OverviewService);
  private readonly repoCtx     = inject(RepositoryContextService);
  private readonly auth        = inject(AuthService);
  private readonly nudges      = inject(NMateNudgeService);
  readonly resource = inject(ResourceService);
  readonly t = this.resource.get.bind(this.resource);
  readonly timeAgo = timeAgo;

  readonly typeScheme = TYPE_SCHEME;
  readonly statusScheme = STATUS_SCHEME;
  readonly velocityScheme = VELOCITY_SCHEME;
  readonly burndownScheme = BURNDOWN_SCHEME;
  readonly workloadScheme = WORKLOAD_SCHEME;

  readonly stats   = this.overviewSvc.stats;
  readonly loading = this.overviewSvc.loading;
  readonly error   = this.overviewSvc.error;

  /** Re-requests the overview after a failed load. */
  retry(): void {
    this.overviewSvc.retryLoad();
  }

  readonly selectedSprintId = this.overviewSvc.selectedSprintId;

  readonly availableSprints = computed(() => this.stats()?.availableSprints ?? []);

  readonly selectedSprintName = computed(() => {
    const id = this.selectedSprintId();
    const sprints = this.availableSprints();
    return (id ? sprints.find(s => s.id === id) : sprints[0])?.name ?? '—';
  });

  readonly sprintTotal      = computed(() => this.stats()?.workItemCounts.total ?? 0);
  readonly sprintInProgress = computed(() => this.stats()?.statusDistribution.active ?? 0);
  readonly sprintDone       = computed(() => this.stats()?.statusDistribution.done ?? 0);

  readonly cycleTime = computed(() => this.stats()?.cycleTime ?? { averageDays: 0, sampleCount: 0 });

  readonly sprintHealth = computed(() => this.stats()?.sprintHealth);

  /** Sprint report card data for the selected sprint. */
  readonly report = computed(() => this.stats()?.report);

  readonly healthBadge = computed(() => {
    const status = this.sprintHealth()?.status ?? 'OnTrack';
    return HEALTH_BADGES[status] ?? HEALTH_BADGES['OnTrack'];
  });

  /** Colour of the stripe along the top of the current-sprint card. */
  readonly healthStripe = computed(() => {
    const status = this.sprintHealth()?.status ?? 'OnTrack';
    return HEALTH_STRIPES[status] ?? HEALTH_STRIPES['OnTrack'];
  });

  /** Problems on the board worth acting on, as judged by the server. */
  readonly attention = computed(() => this.stats()?.attention ?? []);

  /**
   * The loudest severity currently in the strip.
   *
   * The strip takes its colour from the worst item rather than always wearing red: a board whose
   * only problem is a card going stale is not in the same state as one breaching a hard WIP stop,
   * and a banner that looks identical either way stops being read.
   */
  readonly attentionLevel = computed<'warning' | 'critical'>(() =>
    this.attention().some(a => a.severity === 'Critical') ? 'critical' : 'warning',
  );

  constructor() {
    // Offer help with the worst problem on screen, once. The nudge is built from an item the
    // reader can already see, so it reads as NMate noticing the same thing they did rather
    // than as an advert that happens to fire on this route.
    effect(() => {
      const worst = this.attention()[0];
      if (!worst) return;

      untracked(() => this.nudges.offer({
        key:      `overview:${worst.kind}:${worst.targetId ?? worst.message}`,
        message:  `${worst.message}. Want me to look into it?`,
        action:   'Ask NMate',
        question: `${worst.message} — what is causing this and what should we do about it?`,
      }));
    });
  }

  /**
   * Router segments for a page inside the current repository.
   *
   * Attention items name their destination as a bare route ("boards") because the server has
   * no business knowing how the client spells its URLs.
   *
   * @param page Sub-route, e.g. 'boards'.
   * @returns Router link segments.
   */
  repoLink(page: string): string[] {
    const alias = this.auth.currentUser()?.orgAlias ?? '';
    const code  = this.repoCtx.selectedRepo()?.code;
    return code ? ['/', alias, code, ...page.split('/')] : ['/', alias];
  }

  /** The previous sprint's figures, or null on a repository's first sprint. */
  private readonly comparison = computed(() => {
    const c = this.stats()?.comparison;
    return c?.hasPrevious ? c : null;
  });

  /** Change in item count against the previous sprint; null when there is none to compare. */
  readonly totalDelta = computed(() => {
    const previous = this.comparison();
    return previous ? this.sprintTotal() - previous.totalItems : null;
  });

  /** Change in completed items against the previous sprint. */
  readonly doneDelta = computed(() => {
    const previous = this.comparison();
    return previous ? this.sprintDone() - previous.doneItems : null;
  });

  /** Change in average cycle time, rounded to match the precision the figure is shown at. */
  readonly cycleTimeDelta = computed(() => {
    const previous = this.comparison();
    if (!previous || previous.cycleTimeDays === 0) return null;
    return Math.round((this.cycleTime().averageDays - previous.cycleTimeDays) * 10) / 10;
  });

  /** Completed share of committed points, for the progress bar. */
  readonly pointProgress = computed(() => {
    const points = this.storyPoints();
    if (!points || points.committed === 0) return 0;
    return Math.min(100, Math.round((points.completed / points.committed) * 100));
  });

  /**
   * Where a sprint running to plan would be today, as a percentage of committed points.
   *
   * Derived from the burn-down the server already computes rather than from the calendar, so
   * the tick agrees with the chart beside it. Null before a sprint has any burn-down data.
   */
  readonly idealProgress = computed(() => {
    const health = this.sprintHealth();
    const points = this.storyPoints();
    if (!health || !points || points.committed === 0) return null;
    if (health.idealRemaining <= 0 && health.actualRemaining <= 0) return null;

    const burned = points.committed - health.idealRemaining;
    return Math.max(0, Math.min(100, Math.round((burned / points.committed) * 100)));
  });

  /** The selected sprint's date range, as "2026-06-29 → 2026-07-10". */
  readonly sprintRange = computed(() => {
    const id = this.selectedSprintId();
    const sprints = this.availableSprints();
    const sprint = id ? sprints.find(s => s.id === id) : sprints[0];
    if (!sprint?.startDate || !sprint?.endDate) return '';
    return `${sprint.startDate.slice(0, 10)} → ${sprint.endDate.slice(0, 10)}`;
  });

  readonly typeChartData = computed(() =>
    ['UserStory', 'Task', 'Bug', 'TestPlan'].map(type => ({
      name: TYPE_LABELS[type],
      value: this.stats()?.typeDistribution.find(d => d.type === type)?.count ?? 0,
    })),
  );

  readonly statusItems = computed(() => {
    const s = this.stats()?.statusDistribution;
    if (!s) return [];
    return [
      { name: 'To Do', value: s.todo },
      { name: 'Active', value: s.active },
      { name: 'In Review', value: s.inReview },
      { name: 'Done', value: s.done },
    ];
  });

  readonly storyPoints = computed(() => this.stats()?.storyPoints);

  readonly velocityChartSeries = computed(() => {
    const items = this.stats()?.velocityTrend ?? [];
    return [
      { name: 'Committed', series: items.map(i => ({ name: i.sprintName, value: i.committedPoints })) },
      { name: 'Completed', series: items.map(i => ({ name: i.sprintName, value: i.completedPoints })) },
    ];
  });

  readonly burndownChartSeries = computed(() => {
    const ideal  = this.stats()?.burndownData.ideal ?? [];
    const actual = this.stats()?.burndownData.actual ?? [];
    return [
      { name: 'Ideal', series: ideal.map((v, i) => ({ name: `Day ${i + 1}`, value: v })) },
      { name: 'Actual', series: actual.map((v, i) => ({ name: `Day ${i + 1}`, value: v })) },
    ];
  });

  readonly typeTrendChartSeries = computed(() => {
    const items = this.stats()?.typeTrend ?? [];
    return [
      { name: 'User Story', series: items.map(i => ({ name: i.sprintName, value: i.userStory })) },
      { name: 'Task', series: items.map(i => ({ name: i.sprintName, value: i.task })) },
      { name: 'Bug', series: items.map(i => ({ name: i.sprintName, value: i.bug })) },
      { name: 'Test Plan', series: items.map(i => ({ name: i.sprintName, value: i.testPlan })) },
    ];
  });

  readonly assigneeWorkload = computed(() => this.stats()?.assigneeWorkload ?? []);

  readonly assigneeWorkloadChartData = computed(() =>
    this.assigneeWorkload().map(a => ({ name: a.assigneeName, value: a.taskCount })),
  );

  readonly recentActivity = computed(() => this.stats()?.recentActivity ?? []);

  /** Selects a sprint by ID and reloads overview data. @param sprintId Sprint ID. */
  selectSprint(sprintId: string): void {
    this.overviewSvc.selectSprint(sprintId || null);
  }
}
