import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * The change in a headline metric against the previous period.
 *
 * A number on its own does not say whether it is good news — "0.5 days" only means something
 * next to what it was last sprint. Renders nothing when there is no previous period to
 * compare against, so a repository's first sprint shows clean figures rather than fake zeroes.
 */
@Component({
  selector: 'app-metric-delta',
  templateUrl: './metric-delta.component.html',
  styleUrl: './metric-delta.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MetricDeltaComponent {
  /** Change against the previous period, or null when there is nothing to compare with. */
  readonly delta = input<number | null>(null);

  /** Unit shown after the figure, e.g. "days". */
  readonly unit = input('');

  /** Set for metrics where a fall is an improvement, such as cycle time. */
  readonly lowerIsBetter = input(false);

  /** Hidden when there is no comparison, or when nothing changed. */
  readonly visible = computed(() => this.delta() !== null && this.delta() !== 0);

  /** Magnitude, since direction is carried by the arrow. */
  readonly magnitude = computed(() => Math.abs(this.delta() ?? 0));

  readonly rising = computed(() => (this.delta() ?? 0) > 0);

  /**
   * Whether this change is an improvement, which is not the same as whether it rose:
   * more items done is good, a longer cycle time is not.
   */
  readonly favourable = computed(() => (this.lowerIsBetter() ? !this.rising() : this.rising()));
}
