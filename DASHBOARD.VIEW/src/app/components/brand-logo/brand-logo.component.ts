import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * The NFlow mark: a hexagon head with the two strokes of the "N" cut out of it.
 *
 * Drawn as inline SVG rather than served from `logo.png`, because that file is a square
 * tile with its background baked in — on a light surface it shows as a dark box around the
 * hexagon. The shape here is only the hexagon, so it sits on any background.
 */
@Component({
  selector: 'app-brand-logo',
  templateUrl: './brand-logo.component.html',
  styleUrl: './brand-logo.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BrandLogoComponent {
  /** Rendered width and height in pixels. */
  readonly size = input(24);

  /**
   * Set on dark surfaces. The mark's body is near-black, so on the sign-in screens it would
   * otherwise disappear into the background; this gives the hexagon a pale rim instead.
   */
  readonly onDark = input(false);
}
