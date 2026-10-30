import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NMateNudgeComponent } from './nmate-nudge.component';
import { NMateNudge } from '../../models/nmate-nudge.model';

describe('NMateNudgeComponent', () => {
  let fixture: ComponentFixture<NMateNudgeComponent>;
  let component: NMateNudgeComponent;

  const nudge: NMateNudge = {
    key: 'overview:Overloaded:user-7',
    message: 'James is overloaded. Want me to look into it?',
    action: 'Ask NMate',
    question: 'Why is James overloaded?',
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [NMateNudgeComponent] }).compileComponents();
    fixture = TestBed.createComponent(NMateNudgeComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('nudge', nudge);
    fixture.detectChanges();
  });

  it('shows the message and the action label', () => {
    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain(nudge.message);
    expect(text).toContain(nudge.action);
  });

  it('hands the whole nudge back on accept, so the caller has the question too', () => {
    const accepted = jasmine.createSpy('accepted');
    component.accepted.subscribe(accepted);

    fixture.nativeElement.querySelector('.nx-btn--primary').click();

    expect(accepted).toHaveBeenCalledWith(nudge);
  });

  it('dismisses from both the close button and "Not now"', () => {
    const dismissed = jasmine.createSpy('dismissed');
    component.dismissed.subscribe(dismissed);

    fixture.nativeElement.querySelector('.nm-nudge__close').click();
    fixture.nativeElement.querySelector('.nm-nudge__later').click();

    expect(dismissed).toHaveBeenCalledTimes(2);
  });

  it('announces itself politely rather than interrupting', () => {
    // role="status" is read at the next pause; an alert would cut across what is being read.
    expect(fixture.nativeElement.querySelector('.nm-nudge').getAttribute('role')).toBe('status');
  });
});
