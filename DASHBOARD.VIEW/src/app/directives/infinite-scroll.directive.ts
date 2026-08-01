import { AfterViewInit, Directive, ElementRef, inject, input, OnDestroy, output } from '@angular/core';

/**
 * Emits when the host element (a sentinel placed at the end of a list) scrolls into view,
 * so the parent can render the next batch of client-side rows (progressive rendering).
 * Uses IntersectionObserver against the viewport, with a look-ahead margin so loading
 * begins slightly before the sentinel is fully visible.
 */
@Directive({
  selector: '[appInfiniteScroll]',
})
export class InfiniteScrollDirective implements AfterViewInit, OnDestroy {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  /** When true, intersections are ignored (e.g. no more rows to load). */
  readonly disabled = input(false, { alias: 'appInfiniteScrollDisabled' });

  /** Root margin around the viewport; a positive value pre-loads before the sentinel appears. */
  readonly rootMargin = input('300px', { alias: 'appInfiniteScrollRootMargin' });

  /** Emitted when the sentinel enters the viewport and loading is enabled. */
  readonly scrolled = output<void>({ alias: 'appInfiniteScroll' });

  private observer?: IntersectionObserver;

  /** Starts observing the sentinel once it exists in the DOM. */
  ngAfterViewInit(): void {
    this.observer = new IntersectionObserver(
      entries => {
        if (entries[0]?.isIntersecting && !this.disabled()) {
          this.scrolled.emit();
        }
      },
      { rootMargin: this.rootMargin() },
    );
    this.observer.observe(this.host.nativeElement);
  }

  /** Disconnects the observer on teardown. */
  ngOnDestroy(): void {
    this.observer?.disconnect();
  }
}
