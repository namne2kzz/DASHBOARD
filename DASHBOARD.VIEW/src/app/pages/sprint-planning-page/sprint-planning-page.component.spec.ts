import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideToastr } from 'ngx-toastr';
import { provideAnimations } from '@angular/platform-browser/animations';
import { signal, WritableSignal } from '@angular/core';
import { SprintPlanningPageComponent } from './sprint-planning-page.component';
import { PrivilegeService } from '../../core/services/privilege.service';

/** Every permission this screen reads, each one individually settable by a test. */
type Gates = Record<
  'canManageSprints' | 'canManageCapacity' | 'canActivateSprint' | 'canAssignWorkItem' | 'canEditWorkItem',
  WritableSignal<boolean>
>;

describe('SprintPlanningPageComponent', () => {
  let component: SprintPlanningPageComponent;
  let fixture: ComponentFixture<SprintPlanningPageComponent>;
  let gates: Gates;

  beforeEach(async () => {
    // Granted by default so the existing tests see the screen a privileged user sees;
    // the permission tests revoke what they are about.
    gates = {
      canManageSprints:  signal(true),
      canManageCapacity: signal(true),
      canActivateSprint: signal(true),
      canAssignWorkItem: signal(true),
      canEditWorkItem:   signal(true),
    };

    await TestBed.configureTestingModule({
      imports: [SprintPlanningPageComponent],
      providers: [
        provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
        provideToastr(), provideAnimations(),
        { provide: PrivilegeService, useValue: gates },
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

  describe('permission gating', () => {
    /** Pins the selected sprint so the lifecycle predicates have something to judge. */
    function selectSprint(status: 'Planning' | 'Active', calendar: 'past' | 'active' | 'future' = 'active'): void {
      spyOn(component.planning, 'selectedSprint').and.returnValue({ id: 's1', name: 'Sprint 1', status } as never);
      spyOn(component.planning, 'sprintStatus').and.returnValue(calendar);
    }

    it('offers activation only to a role holding ActivateSprint', () => {
      selectSprint('Planning');

      expect(component.canActivateSelectedSprint()).toBeTrue();

      gates.canActivateSprint.set(false);

      expect(component.canActivateSelectedSprint()).toBeFalse();
    });

    it('offers closing only to a role holding ActivateSprint', () => {
      // Activate and Close are one permission precisely because they are the same authority.
      selectSprint('Active');

      expect(component.canCloseSelectedSprint()).toBeTrue();

      gates.canActivateSprint.set(false);

      expect(component.canCloseSelectedSprint()).toBeFalse();
    });

    it('withholds edit and delete from a role without ManageSprint', () => {
      selectSprint('Planning', 'future');

      expect(component.canEditSelectedSprint()).toBeTrue();
      expect(component.canDeleteSelectedSprint()).toBeTrue();

      gates.canManageSprints.set(false);

      expect(component.canEditSelectedSprint()).toBeFalse();
      expect(component.canDeleteSelectedSprint()).toBeFalse();
    });

    it('keeps ManageSprint from standing in for ActivateSprint', () => {
      // The two were conflated in the backend once; nothing here should re-introduce the shortcut.
      selectSprint('Planning', 'future');
      gates.canActivateSprint.set(false);

      expect(component.canEditSelectedSprint()).toBeTrue();
      expect(component.canActivateSelectedSprint()).toBeFalse();
    });

    it('refuses to open the reassign picker without AssignWorkItem', () => {
      gates.canAssignWorkItem.set(false);
      const event = new MouseEvent('click');

      component.openReassign('t1', document.createElement('button'), event);

      expect(component.reassignOpenTaskId()).toBeNull();
    });
  });

  it('hides the rebalancing suggestion once it is waved away', () => {
    expect(component.rebalanceDismissed()).toBeFalse();

    component.dismissRebalance();

    expect(component.rebalanceDismissed()).toBeTrue();
  });
});
