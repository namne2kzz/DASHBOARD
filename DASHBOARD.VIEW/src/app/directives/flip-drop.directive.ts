import { AfterViewInit, Directive, ElementRef, inject } from '@angular/core';

/**
 * Automatically flips a dropdown panel upward when it would overflow the viewport bottom.
 *
 * Usage — place on the absolute-positioned dropdown panel inside an `@if (open())` block:
 * ```html
 * @if (open()) {
 *   <div appFlipDrop class="absolute left-0 top-full z-50 mt-1 ...">...</div>
 * }
 * ```
 * Because the element is conditionally rendered, `ngAfterViewInit` fires each time it
 * enters the DOM, giving a fresh measurement after every open.
 *
 * The directive removes `top-full` / `mt-N` classes and adds `bottom-full` / `mb-1`
 * when the panel bottom would land below the visible viewport (with an 8 px safety margin).
 */
@Directive({
  selector: '[appFlipDrop]',
  standalone: true,
})
export class FlipDropDirective implements AfterViewInit {
  private readonly el = inject<ElementRef<HTMLElement>>(ElementRef);

  /** Runs once after the panel is inserted into the DOM and measures available space. */
  ngAfterViewInit(): void {
    const panel  = this.el.nativeElement;
    const rect   = panel.getBoundingClientRect();
    const margin = 8; // px safety gap from viewport edge

    if (rect.bottom > window.innerHeight - margin) {
      // Not enough room below — flip above the trigger.
      panel.classList.remove('top-full', 'mt-1', 'mt-2', 'mt-3');
      panel.classList.add('bottom-full', 'mb-1');
    }
  }
}
