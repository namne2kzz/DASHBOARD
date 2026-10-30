import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { NMateLauncherComponent } from './nmate-launcher.component';

describe('NMateLauncherComponent', () => {
  let fixture: ComponentFixture<NMateLauncherComponent>;
  let component: NMateLauncherComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NMateLauncherComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(NMateLauncherComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  describe('screen-reader label', () => {
    it('invites you in at rest', () => {
      expect(component.fabLabel()).toContain('Open NMate');
    });

    it('says the panel will close when it is open', () => {
      component.nmate.open();
      expect(component.fabLabel()).toBe('Close NMate');
    });

    it('counts unread replies, since the badge is invisible to a screen reader', () => {
      component.nmate.unreadCount.set(1);
      expect(component.fabLabel()).toContain('1 new reply');

      component.nmate.unreadCount.set(3);
      expect(component.fabLabel()).toContain('3 new replies');
    });
  });

  it('takes a coral rim only while an offer is on screen', () => {
    const button: HTMLElement = fixture.nativeElement.querySelector('.nm-fab');
    expect(button.classList).not.toContain('has-nudge');

    fixture.componentRef.setInput('hasNudge', true);
    fixture.detectChanges();
    expect(button.classList).toContain('has-nudge');
  });
});
