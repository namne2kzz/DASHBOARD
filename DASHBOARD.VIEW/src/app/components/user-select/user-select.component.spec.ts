import { ComponentFixture, TestBed } from '@angular/core/testing';
import { UserSelectComponent, UserOption } from './user-select.component';

/**
 * The picker is used wherever a member is chosen, including places the current user may read but
 * not change — reassigning a task needs AssignWorkItem, setting a manager needs global admin.
 * These tests cover the disabled path that those screens rely on: the selection stays legible,
 * but the dropdown refuses to open.
 */
describe('UserSelectComponent', () => {
  let fixture: ComponentFixture<UserSelectComponent>;
  let component: UserSelectComponent;

  const users: UserOption[] = [
    { id: 'u1', name: 'Ada Lovelace',  avatarClass: 'bg-sky-500'  },
    { id: 'u2', name: 'Alan Turing',   avatarClass: 'bg-rose-500' },
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [UserSelectComponent] }).compileComponents();

    fixture   = TestBed.createComponent(UserSelectComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('users', users);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('opens on click when enabled', () => {
    component.toggle();

    expect(component.open()).toBeTrue();
  });

  it('refuses to open when disabled', () => {
    fixture.componentRef.setInput('disabled', true);

    component.toggle();

    expect(component.open()).toBeFalse();
  });

  it('still reports who is selected when disabled', () => {
    // The whole point of disabling rather than hiding: the assignee stays readable.
    fixture.componentRef.setInput('selectedId', 'u1');
    fixture.componentRef.setInput('disabled', true);

    expect(component.selectedUser()?.name).toBe('Ada Lovelace');
  });

  it('marks the trigger button disabled so the browser skips it in the tab order', () => {
    fixture.componentRef.setInput('disabled', true);
    fixture.detectChanges();

    const trigger = fixture.nativeElement.querySelector('button') as HTMLButtonElement;

    expect(trigger.disabled).toBeTrue();
  });

  it('filters the list by name', () => {
    component.search.set('turing');

    expect(component.filtered().map(u => u.id)).toEqual(['u2']);
  });
});
