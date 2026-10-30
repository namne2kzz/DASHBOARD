import {
  Directive, ElementRef, afterNextRender, inject, signal, OnDestroy,
} from '@angular/core';

/** How close to an edge the pointer must come, in px, before the view starts moving. */
const EDGE_ZONE = 110;

/** Fastest auto-scroll, in px per frame, reached at the very edge. */
const MAX_STEP = 22;

/**
 * Makes a horizontally scrolling container navigable without its scrollbar.
 *
 * Two things the board needed and a plain `overflow-x: auto` does not give:
 *
 * 1. **Paging buttons.** The board's scrollbar sits at the bottom of a tall column list, often
 *    below the fold, so reaching it means scrolling vertically first. `canScrollLeft` /
 *    `canScrollRight` drive arrow buttons that page by roughly one screen.
 *
 * 2. **Auto-scroll while dragging.** Dragging a card to the edge scrolls the board towards the
 *    columns off-screen, so a card can reach a column the viewport never showed. The CDK scrolls
 *    the list a card is *over*, which cannot help when the target column is not rendered near the
 *    pointer. Speed ramps with depth into the edge zone so a small correction stays controllable.
 *
 * The host keeps ownership of its own scrolling; this only reads geometry and sets `scrollLeft`.
 */
@Directive({
  selector: '[appHorizontalPager]',
  exportAs: 'horizontalPager',
})
export class HorizontalPagerDirective implements OnDestroy {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  /** True while there is content hidden to the left. */
  readonly canScrollLeft = signal(false);

  /** True while there is content hidden to the right. */
  readonly canScrollRight = signal(false);

  private frame: number | null = null;
  private velocity = 0;
  private observer?: ResizeObserver;

  constructor() {
    const el = this.host.nativeElement;

    el.addEventListener('scroll', this.sync, { passive: true });

    // Columns are added, filtered and resized after first paint, and each changes whether there is
    // anything left to page to — so watch the element rather than measuring once.
    this.observer = new ResizeObserver(this.sync);
    this.observer.observe(el);

    afterNextRender(() => this.sync());
  }

  /** Stops the drag loop and releases the observer. */
  ngOnDestroy(): void {
    this.stopAutoScroll();
    this.observer?.disconnect();
    this.host.nativeElement.removeEventListener('scroll', this.sync);
  }

  /**
   * Pages one screen towards the given side, less a sliver kept for context.
   * @param direction -1 for left, 1 for right.
   */
  page(direction: -1 | 1): void {
    const el = this.host.nativeElement;
    // Overlapping by ~15% keeps a column visible across the jump, so the eye has an anchor and
    // nothing appears to have been skipped.
    const step = Math.max(240, el.clientWidth * 0.85);
    el.scrollBy({ left: direction * step, behavior: 'smooth' });
  }

  /**
   * Feeds a pointer position to the drag auto-scroller.
   * @param clientX Pointer x in viewport coordinates, or null when the drag ends.
   */
  trackPointer(clientX: number | null): void {
    if (clientX === null) {
      this.stopAutoScroll();
      return;
    }

    const { left, right } = this.host.nativeElement.getBoundingClientRect();
    const intoLeft = clientX - left;
    const intoRight = right - clientX;

    // Depth into the zone, 0 at its inner boundary and 1 at the edge itself, so the board creeps
    // when the card is merely near the edge and moves briskly when it is pinned against it.
    if (intoLeft < EDGE_ZONE) {
      this.velocity = -MAX_STEP * this.ramp((EDGE_ZONE - intoLeft) / EDGE_ZONE);
    } else if (intoRight < EDGE_ZONE) {
      this.velocity = MAX_STEP * this.ramp((EDGE_ZONE - intoRight) / EDGE_ZONE);
    } else {
      this.velocity = 0;
    }

    if (this.velocity === 0) this.stopAutoScroll();
    else this.startAutoScroll();
  }

  /** Squares the 0–1 depth so the slow end of the range is wider and easier to hold. */
  private ramp(depth: number): number {
    const clamped = Math.min(1, Math.max(0, depth));
    return clamped * clamped;
  }

  private startAutoScroll(): void {
    if (this.frame !== null) return;

    const step = () => {
      const el = this.host.nativeElement;
      const before = el.scrollLeft;
      el.scrollLeft = before + this.velocity;

      // Hitting either end means there is nothing more to reveal; stop rather than burn frames.
      if (el.scrollLeft === before) {
        this.stopAutoScroll();
        return;
      }

      this.frame = requestAnimationFrame(step);
    };

    this.frame = requestAnimationFrame(step);
  }

  private stopAutoScroll(): void {
    if (this.frame !== null) cancelAnimationFrame(this.frame);
    this.frame = null;
    this.velocity = 0;
  }

  /** Recomputes which sides still have content, tolerating sub-pixel scroll positions. */
  private readonly sync = (): void => {
    const el = this.host.nativeElement;
    const max = el.scrollWidth - el.clientWidth;
    this.canScrollLeft.set(el.scrollLeft > 1);
    this.canScrollRight.set(el.scrollLeft < max - 1);
  };
}
