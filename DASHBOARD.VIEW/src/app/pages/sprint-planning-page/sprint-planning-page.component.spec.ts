import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideToastr } from 'ngx-toastr';
import { provideAnimations } from '@angular/platform-browser/animations';
import { SprintPlanningPageComponent } from './sprint-planning-page.component';

describe('SprintPlanningPageComponent', () => {
  let component: SprintPlanningPageComponent;
  let fixture: ComponentFixture<SprintPlanningPageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SprintPlanningPageComponent],
      providers: [
        provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
        provideToastr(), provideAnimations(),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(SprintPlanningPageComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('load meter', () => {
    it('puts an empty load at zero width', () => {
      expect(component.loadFillWidth(0)).toBe(0);
      expect(component.loadOverWidth(0)).toBe(0);
    });

    it('fills exactly to the 100% tick at full capacity', () => {
      // The whole point of the marker: "at capacity" must land on it, not past it.
      expect(component.loadFillWidth(100)).toBe(component.LOAD_MARKER_AT);
      expect(component.loadOverWidth(100)).toBe(0);
    });

    it('stops the violet fill at the tick however overloaded someone is', () => {
      expect(component.loadFillWidth(125)).toBe(component.LOAD_MARKER_AT);
      expect(component.loadFillWidth(300)).toBe(component.LOAD_MARKER_AT);
    });

    it('grows the red overflow with the load past capacity', () => {
      const at125 = component.loadOverWidth(125);
      const at140 = component.loadOverWidth(140);

      expect(at125).toBeGreaterThan(0);
      expect(at140).toBeGreaterThan(at125);
    });

    it('never lets the overflow run past the end of the bar', () => {
      // Someone at 400% would otherwise draw a segment wider than the meter itself.
      const room = 100 - component.LOAD_MARKER_AT;
      expect(component.loadOverWidth(400)).toBeLessThanOrEqual(room);
    });

    it('keeps the fill and the overflow within the bar together', () => {
      expect(component.loadFillWidth(180) + component.loadOverWidth(180)).toBeLessThanOrEqual(100);
    });
  });

  it('hides the rebalancing suggestion once it is waved away', () => {
    expect(component.rebalanceDismissed()).toBeFalse();

    component.dismissRebalance();

    expect(component.rebalanceDismissed()).toBeTrue();
  });
});
