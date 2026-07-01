import { CommonModule } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { ResourceService } from '../../services/resource.service';
import { OverviewService } from '../../services/overview.service';

const TYPE_COLORS: Record<string, string> = {
  UserStory: '#38bdf8',
  Bug:       '#fb7185',
  Task:      '#fbbf24',
  TestPlan:  '#a855f7',
};

const STATUS_COLORS: Record<string, string> = {
  todo:     '#38bdf8',
  active:   '#f59e0b',
  inReview: '#fb923c',
  done:     '#22c55e',
};

@Component({
  selector: 'app-overview-page',
  imports: [CommonModule],
  templateUrl: './overview-page.component.html',
  styleUrl: './overview-page.component.css',
})
export class OverviewPageComponent {
  private readonly overviewSvc = inject(OverviewService);
  readonly resource = inject(ResourceService);
  readonly t = this.resource.get.bind(this.resource);

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

  readonly bugCount       = computed(() => this.stats()?.workItemCounts.bugs ?? 0);
  readonly userStoryCount = computed(() => this.stats()?.workItemCounts.userStories ?? 0);
  readonly taskCount      = computed(() => this.stats()?.workItemCounts.tasks ?? 0);
  readonly testPlanCount  = computed(() => this.stats()?.workItemCounts.testPlans ?? 0);

  readonly bugVsStoryMax  = computed(() => Math.max(1, this.bugCount() + this.userStoryCount()));
  readonly taskVsTestMax  = computed(() => Math.max(1, this.taskCount() + this.testPlanCount()));

  readonly statusItems = computed(() => {
    const s = this.stats()?.statusDistribution;
    if (!s) return [];
    return [
      { label: 'To Do',     count: s.todo,     color: STATUS_COLORS['todo'] },
      { label: 'Active',    count: s.active,   color: STATUS_COLORS['active'] },
      { label: 'In Review', count: s.inReview, color: STATUS_COLORS['inReview'] },
      { label: 'Done',      count: s.done,     color: STATUS_COLORS['done'] },
    ];
  });

  readonly statusMax = computed(() => Math.max(1, ...this.statusItems().map(i => i.count)));

  readonly typeDistribution = computed(() =>
    (this.stats()?.typeDistribution ?? []).map(item => ({
      ...item,
      label: item.type,
      color: TYPE_COLORS[item.type] ?? '#64748b',
    })),
  );

  readonly typeTotal = computed(() =>
    this.typeDistribution().reduce((sum, item) => sum + item.count, 0),
  );

  readonly typePieSegments = computed(() => {
    const total = Math.max(1, this.typeTotal());
    const circumference = 2 * Math.PI * 36;
    let offset = 0;
    return this.typeDistribution().map(segment => {
      const length = (segment.count / total) * circumference;
      const result = {
        ...segment,
        dashArray: `${length} ${circumference}`,
        offset,
        percentage: (segment.count / total) * 100,
      };
      offset += length;
      return result;
    });
  });

  readonly velocityTrend = computed(() => this.stats()?.velocityTrend ?? []);

  readonly sprintTrendItems = computed(() => {
    const items = this.velocityTrend();
    const step = items.length > 1 ? 272 / (items.length - 1) : 0;
    return items.map((item, index) => ({ ...item, x: 32 + step * index }));
  });

  readonly sprintTrend = computed(() => this.sprintTrendItems().map(i => i.committedPoints));

  readonly storyPoints   = computed(() => this.stats()?.storyPoints);
  readonly burndownIdeal = computed(() => this.stats()?.burndownData.ideal ?? []);
  readonly burndownActual = computed(() => this.stats()?.burndownData.actual ?? []);

  /** Selects a sprint by ID and reloads overview data. @param sprintId Sprint ID. */
  selectSprint(sprintId: string): void {
    this.overviewSvc.selectSprint(sprintId || null);
  }

  /** Builds SVG polyline points string for a chart. @param values Data array. @param w SVG viewport width. @param h SVG viewport height. @param pad Padding on all sides. @returns Space-separated "x,y" coordinate pairs. */
  burndownPolyline(values: number[], w: number, h: number, pad: number): string {
    if (values.length === 0) return '';
    const max    = Math.max(...values, 1);
    const innerW = w - pad * 2;
    const innerH = h - pad * 2;
    return values
      .map((v, i) => {
        const x = pad + (innerW * i) / (values.length - 1 || 1);
        const y = pad + innerH - (innerH * v) / max;
        return `${x.toFixed(1)},${y.toFixed(1)}`;
      })
      .join(' ');
  }
}
