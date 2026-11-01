import { Component, viewChild } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HorizontalPagerDirective } from './horizontal-pager.directive';

/**
 * Unit tests for the board's horizontal pager.
 *
 * The directive exists because the board's scrollbar sits below the fold, so both halves of it —
 * the arrow buttons and the drag auto-scroll — are the only way some columns can be reached. The
 * tests below pin the behaviour that decides whether a column is reachable at all.
 */
@Component({
  imports: [HorizontalPagerDirective],
  template: `
    <div appHorizontalPager style="width: 300px; overflow-x: auto">
      <div style="width: 1200px">wide</div>
    </div>
  `,
})
class HostComponent {
  readonly pager = viewChild.required(HorizontalPagerDirective);
}

describe('HorizontalPagerDirective', () => {
  let fixture: ComponentFixture<HostComponent>;
  let host: HostComponent;
  let el: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [HostComponent] }).compileComponents();
    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
    fixture.detectChanges();
    el = (fixture.nativeElement as HTMLElement).querySelector('div')!;
  });

  /** Replaces the element's scroll geometry, which jsdom does not lay out on its own. */
  function geometry(scrollLeft: number, clientWidth = 300, scrollWidth = 1200): void {
    Object.defineProperty(el, 'clientWidth', { value: clientWidth, configurable: true });
    Object.defineProperty(el, 'scrollWidth', { value: scrollWidth, configurable: true });
    el.scrollLeft = scrollLeft;
    el.dispatchEvent(new Event('scroll'));
  }

  describe('which arrows are offered', () => {
    it('offers only the right arrow at the start', () => {
      geometry(0);

      expect(host.pager().canScrollLeft()).toBeFalse();
      expect(host.pager().canScrollRight()).toBeTrue();
    });

    it('offers both arrows in the middle', () => {
      geometry(400);

      expect(host.pager().canScrollLeft()).toBeTrue();
      expect(host.pager().canScrollRight()).toBeTrue();
    });

    it('offers only the left arrow at the end', () => {
      geometry(900);

      expect(host.pager().canScrollLeft()).toBeTrue();
      expect(host.pager().canScrollRight()).toBeFalse();
    });

    it('offers neither when everything already fits', () => {
      // A board narrow enough to show every column has nothing to page to, and an arrow that
      // does nothing is worse than no arrow.
      geometry(0, 1200, 1200);

      expect(host.pager().canScrollLeft()).toBeFalse();
      expect(host.pager().canScrollRight()).toBeFalse();
    });

    it('treats a sub-pixel scroll position as the end', () => {
      // Fractional scrollLeft is normal after a smooth scroll on a HiDPI display; without the
      // tolerance the right arrow would linger with nothing left to reveal.
      geometry(900.4);

      expect(host.pager().canScrollRight()).toBeFalse();
    });
  });

  describe('paging by button', () => {
    it('moves most of a screen, keeping an overlap', () => {
      geometry(0);
      const scrollBy = spyOn(el, 'scrollBy') as unknown as jasmine.Spy<(opts: ScrollToOptions) => void>;

      host.pager().page(1);

      // 85% of 300px: far enough to be worth a click, short enough that a column stays on screen
      // across the jump.
      expect(scrollBy).toHaveBeenCalledWith({ left: 255, behavior: 'smooth' });
    });

    it('pages backwards with the same step', () => {
      geometry(600);
      const scrollBy = spyOn(el, 'scrollBy') as unknown as jasmine.Spy<(opts: ScrollToOptions) => void>;

      host.pager().page(-1);

      expect(scrollBy).toHaveBeenCalledWith({ left: -255, behavior: 'smooth' });
    });

    it('still moves a usable distance in a very narrow viewport', () => {
      // 85% of a tiny width would be a step too small to feel like anything happened.
      geometry(0, 120, 1200);
      const scrollBy = spyOn(el, 'scrollBy') as unknown as jasmine.Spy<(opts: ScrollToOptions) => void>;

      host.pager().page(1);

      expect(scrollBy).toHaveBeenCalledWith({ left: 240, behavior: 'smooth' });
    });
  });

  describe('auto-scroll while dragging', () => {
    beforeEach(() => {
      spyOn(el, 'getBoundingClientRect').and.returnValue(
        { left: 0, right: 300, top: 0, bottom: 500, width: 300, height: 500, x: 0, y: 0, toJSON: () => ({}) },
      );
      geometry(400);
    });

    it('does nothing while the pointer is away from both edges', () => {
      // Most of a drag happens here, and a board that drifted whenever the card was merely in
      // motion would be unusable.
      const raf = spyOn(window, 'requestAnimationFrame');
      const before = el.scrollLeft;

      host.pager().trackPointer(150);

      expect(raf).not.toHaveBeenCalled();
      expect(el.scrollLeft).toBe(before);
    });

    it('scrolls right when the pointer nears the right edge', () => {
      const before = el.scrollLeft;
      spyOn(window, 'requestAnimationFrame').and.callFake((cb: FrameRequestCallback) => { cb(0); return 1; });

      host.pager().trackPointer(295);

      expect(el.scrollLeft).toBeGreaterThan(before);
    });

    it('scrolls left when the pointer nears the left edge', () => {
      const before = el.scrollLeft;
      spyOn(window, 'requestAnimationFrame').and.callFake((cb: FrameRequestCallback) => { cb(0); return 1; });

      host.pager().trackPointer(5);

      expect(el.scrollLeft).toBeLessThan(before);
    });

    it('moves faster the deeper the pointer goes into the edge zone', () => {
      // The ramp is what makes a small correction controllable: just inside the zone the board
      // should creep, and only against the edge itself should it move at full speed.
      // Held in an array because assigning inside the spy narrows a plain `let` to null for the
      // rest of the closure, leaving nothing callable.
      const pending: FrameRequestCallback[] = [];
      spyOn(window, 'requestAnimationFrame').and.callFake((cb: FrameRequestCallback) => {
        if (pending.length === 0) pending.push(cb);
        return 1;
      });

      /** One frame's travel for a pointer at this x. */
      const step = (clientX: number): number => {
        pending.length = 0;
        host.pager().trackPointer(null);
        geometry(400);

        host.pager().trackPointer(clientX);
        const start = el.scrollLeft;
        pending[0]?.(0);
        return el.scrollLeft - start;
      };

      // 240 is just inside the 110px zone (60px from the edge); 299 is against it.
      const shallow = step(240);
      const deep = step(299);

      expect(shallow).toBeGreaterThan(0);
      expect(deep).toBeGreaterThan(shallow);
      expect(deep).toBeLessThanOrEqual(22);
    });

    it('gives up once the board has nothing left to reveal', () => {
      // Already at the right-hand end: continuing to spin a rAF loop that cannot move anything
      // would burn frames for the rest of the drag.
      geometry(900);
      const cancel = spyOn(window, 'cancelAnimationFrame');

      // The callback is queued, not run inline. A real rAF returns its handle before the frame
      // fires, which is what lets the directive record the handle it later cancels; running the
      // callback inside the call instead means the cancel happens while the handle is still
      // unassigned, and the directive's "nothing to cancel" guard skips it.
      let frame: FrameRequestCallback | null = null;
      spyOn(window, 'requestAnimationFrame').and.callFake((cb: FrameRequestCallback) => {
        frame = cb;
        return 1;
      });

      host.pager().trackPointer(295);
      frame!(0);

      expect(cancel).toHaveBeenCalled();
    });

    it('stops when the drag ends', () => {
      spyOn(window, 'requestAnimationFrame').and.callFake((cb: FrameRequestCallback) => { cb(0); return 1; });
      const cancel = spyOn(window, 'cancelAnimationFrame');

      host.pager().trackPointer(295);
      host.pager().trackPointer(null);

      expect(cancel).toHaveBeenCalled();
    });
  });
});
