import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { NMateWidgetComponent } from './nmate-widget.component';
import { NMateService } from '../../services/nmate.service';
import { NMateMessage } from '../../models/nmate.model';
import { PreferencesService } from '../../core/services/preferences.service';

/**
 * Unit tests for NMateWidgetComponent. The service is replaced by a signal-backed fake so the tests
 * drive exactly the states the template branches on: hidden, closed, empty with suggestions, streaming.
 */
describe('NMateWidgetComponent', () => {
  let fixture: ComponentFixture<NMateWidgetComponent>;
  let el: HTMLElement;

  /** The user's Settings → General preference for showing NMate. */
  const prefs = { showNMate: signal(true) };

  const fake = {
    isOpen: signal(false),
    enabled: signal<boolean | null>(true),
    available: signal<boolean | null>(true),
    messages: signal<NMateMessage[]>([]),
    suggestions: signal<string[]>(['Làm sao tạo sprint mới?']),
    isStreaming: signal(false),
    conversationId: signal<string | null>('c2'),
    recentConversations: signal<{ id: string; title: string; lastMessageAt: string }[]>([]),
    historyLoading: signal(false),
    conversationLoading: signal(false),
    loadRecentConversations: jasmine.createSpy('loadRecentConversations'),
    openConversation: jasmine.createSpy('openConversation'),
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
    prefs.showNMate.set(true);
    fake.enabled.set(true);
    fake.isStreaming.set(false);
    fake.messages.set([]);
    // The fake is shared and Jasmine runs specs in random order, so every signal a spec may change is reset here.
    fake.available.set(true);
    fake.conversationId.set('c2');
    fake.historyLoading.set(false);
    fake.conversationLoading.set(false);
    fake.recentConversations.set([
      { id: 'c1', title: 'Làm sao đóng sprint?', lastMessageAt: new Date().toISOString() },
      { id: 'c2', title: 'Promote to Sprint là gì?', lastMessageAt: new Date().toISOString() },
    ]);
    Object.values(fake).forEach(v => (v as jasmine.Spy).calls?.reset());

    TestBed.configureTestingModule({
      imports: [NMateWidgetComponent],
      providers: [
        provideRouter([]),
        { provide: NMateService, useValue: fake },
        { provide: PreferencesService, useValue: prefs },
      ],
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

  it('clock button opens the recent conversations, highlighting the current one', () => {
    fake.isOpen.set(true);
    fixture.detectChanges();

    (el.querySelector('.nm-history-toggle') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(fake.loadRecentConversations).toHaveBeenCalledWith(3);
    const items = el.querySelectorAll('.nm-history__item');
    expect(items.length).toBe(2);
    expect(items[1].classList).toContain('is-current');
    expect(items[0].textContent).toContain('just now');
  });

  it('picking a conversation continues it and closes the menu', () => {
    fake.isOpen.set(true);
    fixture.detectChanges();
    (el.querySelector('.nm-history-toggle') as HTMLButtonElement).click();
    fixture.detectChanges();

    (el.querySelector('.nm-history__item') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(fake.openConversation).toHaveBeenCalledWith('c1');
    expect(el.querySelector('.nm-history')).toBeNull();
  });

  it('Escape closes the menu first, the panel on the next press; outside click also closes it', () => {
    fake.isOpen.set(true);
    fixture.detectChanges();
    (el.querySelector('.nm-history-toggle') as HTMLButtonElement).click();
    fixture.detectChanges();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();
    expect(el.querySelector('.nm-history')).toBeNull();
    expect(fake.close).not.toHaveBeenCalled();

    (el.querySelector('.nm-history-toggle') as HTMLButtonElement).click();
    fixture.detectChanges();
    document.body.click();
    fixture.detectChanges();
    expect(el.querySelector('.nm-history')).toBeNull();
  });

  it('shows an empty state when there is no earlier conversation', () => {
    fake.isOpen.set(true);
    fake.recentConversations.set([]);
    fixture.detectChanges();
    (el.querySelector('.nm-history-toggle') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(el.querySelector('.nm-history__empty')?.textContent).toContain('Chưa có');
  });

  it('renders nothing and ignores Ctrl + / when the user hid NMate in Settings', () => {
    prefs.showNMate.set(false);
    fixture.detectChanges();

    expect(el.querySelector('.nm-fab')).toBeNull();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: '/', ctrlKey: true }));
    expect(fake.toggle).not.toHaveBeenCalled();
  });

  it('hiding NMate while the panel is open closes the panel', () => {
    fake.isOpen.set(true);
    fixture.detectChanges();

    prefs.showNMate.set(false);
    fixture.detectChanges();

    expect(fake.close).toHaveBeenCalled();
  });
});
