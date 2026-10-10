import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { NMateWidgetComponent } from './nmate-widget.component';
import { NMateService } from '../../services/nmate.service';
import { NMateMessage } from '../../models/nmate.model';

/**
 * Unit tests for NMateWidgetComponent. The service is replaced by a signal-backed fake so the tests
 * drive exactly the states the template branches on: hidden, closed, empty with suggestions, streaming.
 */
describe('NMateWidgetComponent', () => {
  let fixture: ComponentFixture<NMateWidgetComponent>;
  let el: HTMLElement;

  const fake = {
    isOpen: signal(false),
    enabled: signal<boolean | null>(true),
    available: signal<boolean | null>(true),
    messages: signal<NMateMessage[]>([]),
    suggestions: signal<string[]>(['Làm sao tạo sprint mới?']),
    isStreaming: signal(false),
    open: jasmine.createSpy('open'),
    close: jasmine.createSpy('close'),
    toggle: jasmine.createSpy('toggle'),
    ask: jasmine.createSpy('ask'),
    stop: jasmine.createSpy('stop'),
    rate: jasmine.createSpy('rate'),
    newConversation: jasmine.createSpy('newConversation'),
    loadSuggestions: jasmine.createSpy('loadSuggestions'),
    citationLink: () => null,
    errorText: () => '',
  };

  beforeEach(() => {
    fake.isOpen.set(false);
    fake.enabled.set(true);
    fake.isStreaming.set(false);
    fake.messages.set([]);
    Object.values(fake).forEach(v => (v as jasmine.Spy).calls?.reset());

    TestBed.configureTestingModule({
      imports: [NMateWidgetComponent],
      providers: [provideRouter([]), { provide: NMateService, useValue: fake }],
    });
    fixture = TestBed.createComponent(NMateWidgetComponent);
    el = fixture.nativeElement;
    fixture.detectChanges();
  });

  it('shows only the floating button while closed', () => {
    expect(el.querySelector('.nm-fab')).not.toBeNull();
    expect(el.querySelector('.nm-panel')).toBeNull();
  });

  it('renders nothing when NMate is switched off on the backend', () => {
    fake.enabled.set(false);
    fixture.detectChanges();

    expect(el.querySelector('.nm-fab')).toBeNull();
  });

  it('toggles with Ctrl + /', () => {
    document.dispatchEvent(new KeyboardEvent('keydown', { key: '/', ctrlKey: true }));

    expect(fake.toggle).toHaveBeenCalled();
  });

  it('shows suggestions when open and empty, and asks one on click', () => {
    fake.isOpen.set(true);
    fixture.detectChanges();

    expect(fake.loadSuggestions).toHaveBeenCalled();
    (el.querySelector('.nm-suggestion') as HTMLButtonElement).click();
    expect(fake.ask).toHaveBeenCalledWith('Làm sao tạo sprint mới?', jasmine.any(String));
  });

  it('sends on Enter and clears the input; Shift+Enter does not send', () => {
    fake.isOpen.set(true);
    fixture.detectChanges();
    const input = el.querySelector('textarea') as HTMLTextAreaElement;

    input.value = 'Câu hỏi';
    input.dispatchEvent(new Event('input'));
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', shiftKey: true }));
    expect(fake.ask).not.toHaveBeenCalled();

    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    expect(fake.ask).toHaveBeenCalledWith('Câu hỏi', jasmine.any(String));
    expect(fixture.componentInstance.draft()).toBe('');
  });

  it('swaps Send for Stop while an answer is streaming', () => {
    fake.isOpen.set(true);
    fake.isStreaming.set(true);
    fixture.detectChanges();

    const stop = Array.from(el.querySelectorAll('.nm-footer button')).find(b => b.textContent?.includes('Dừng')) as HTMLButtonElement;
    stop.click();
    expect(fake.stop).toHaveBeenCalled();
  });

  it('shows the maintenance notice when NMate is unavailable', () => {
    fake.isOpen.set(true);
    fake.available.set(false);
    fixture.detectChanges();

    expect(el.querySelector('.nm-maintenance')).not.toBeNull();
    fake.available.set(true);
  });
});
