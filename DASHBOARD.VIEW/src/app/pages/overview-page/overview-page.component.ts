import { CommonModule } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { Color, NgxChartsModule, ScaleType } from '@swimlane/ngx-charts';
import { ResourceService } from '../../services/resource.service';
import { OverviewService } from '../../services/overview.service';
import { timeAgo } from '../../utils/time-ago.util';

const TYPE_LABELS: Record<string, string> = {
  UserStory: 'User Story',
  Task: 'Task',
  Bug: 'Bug',
  TestPlan: 'Test Plan',
};

// Chart palettes: ngx-charts writes fills as SVG attributes (no CSS vars), so these are
// mid-tone values of the Nexus UI type/state colours that read on both themes.
const TYPE_SCHEME: Color = {
  name: 'type',
  selectable: true,
  group: ScaleType.Ordinal,
  domain: ['#3B6FD4', '#B7791F', '#C33A2E', '#7550C9'], // type-story · type-task · type-bug · type-test
};

const STATUS_SCHEME: Color = {
  name: 'status',
  selectable: true,
  group: ScaleType.Ordinal,
  domain: ['#7D8490', '#3B6FD4', '#B7791F', '#2E9A63'], // New · Active · In Review · Done
};

const VELOCITY_SCHEME: Color = {
  name: 'velocity',
  selectable: true,
  group: ScaleType.Ordinal,
  domain: ['#7D8490', '#2E9A63'], // committed (neutral) · completed (accent)
};

const BURNDOWN_SCHEME: Color = {
  name: 'burndown',
  selectable: true,
  group: ScaleType.Ordinal,
  domain: ['#7D8490', '#2E9A63'], // ideal (neutral) · actual (accent)
};

const WORKLOAD_SCHEME: Color = {
  name: 'workload',
  selectable: true,
  group: ScaleType.Ordinal,
  domain: ['#2E9A63'],
};

const HEALTH_BADGES: Record<string, { label: string; classes: string }> = {
  OnTrack: { label: 'On track', classes: 'bg-emerald-500/10 text-emerald-500 ring-transparent' },
  AtRisk: { label: 'At risk', classes: 'bg-amber-500/10 text-amber-500 ring-transparent' },
  Behind: { label: 'Behind', classes: 'bg-rose-500/10 text-rose-500 ring-transparent' },
};

@Component({
  selector: 'app-overview-page',
  imports: [CommonModule, NgxChartsModule],
  templateUrl: './overview-page.component.html',
  styleUrl: './overview-page.component.css',
})
export class OverviewPageComponent {
  private readonly overviewSvc = inject(OverviewService);
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

  readonly healthBadge = computed(() => {
    const status = this.sprintHealth()?.status ?? 'OnTrack';
    return HEALTH_BADGES[status] ?? HEALTH_BADGES['OnTrack'];
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
