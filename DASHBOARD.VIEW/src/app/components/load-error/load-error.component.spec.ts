import { ComponentFixture, TestBed } from '@angular/core/testing';
import { LoadErrorComponent } from './load-error.component';

describe('LoadErrorComponent', () => {
  let fixture: ComponentFixture<LoadErrorComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [LoadErrorComponent] }).compileComponents();
    fixture = TestBed.createComponent(LoadErrorComponent);
    fixture.componentRef.setInput('message', 'Failed to load things.');
    fixture.detectChanges();
  });

  it('renders the message as an alert', () => {
    const alert: HTMLElement = fixture.nativeElement.querySelector('[role="alert"]');
    expect(alert.textContent).toContain('Failed to load things.');
  });

  it('emits retry when "Try again" is clicked', () => {
    let retried = 0;
    fixture.componentInstance.retry.subscribe(() => retried++);
    fixture.nativeElement.querySelector('button').click();
    expect(retried).toBe(1);
  });

  it('hides the button when the error is not retryable', () => {
    fixture.componentRef.setInput('retryable', false);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('button')).toBeNull();
  });
});
