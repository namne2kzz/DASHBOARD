import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { NMateMessageComponent } from './nmate-message.component';
import { NMateService } from '../../services/nmate.service';
import { NMateMessage } from '../../models/nmate.model';

/** Unit tests for NMateMessageComponent — one chat bubble. */
describe('NMateMessageComponent', () => {
  let fixture: ComponentFixture<NMateMessageComponent>;
  let nmate: jasmine.SpyObj<NMateService>;

  const answer = (overrides: Partial<NMateMessage> = {}): NMateMessage => ({
    localId: 'l1',
    id: 'm1',
    role: 'assistant',
    content: '1. Vào **Sprint Planning**\n2. Bấm **New sprint**',
    citations: [
      { chunkId: 'k1', title: 'Quản lý Sprint', headingPath: 'Tạo sprint', route: '/sprint-planning', score: 0.8 },
      { chunkId: 'k2', title: 'Quản lý Sprint', headingPath: 'Tạo sprint', route: '/sprint-planning', score: 0.7 },
    ],
    status: 'done',
    rating: null,
    ...overrides,
  });

  function render(message: NMateMessage): HTMLElement {
    fixture.componentRef.setInput('message', message);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  beforeEach(() => {
    nmate = jasmine.createSpyObj<NMateService>('NMateService', ['citationLink', 'errorText']);
    nmate.citationLink.and.returnValue('/acme/DASH/sprint-planning');
    nmate.errorText.and.returnValue('NMate tạm hết lượt sử dụng');

    TestBed.configureTestingModule({
      imports: [NMateMessageComponent],
      providers: [provideRouter([]), { provide: NMateService, useValue: nmate }],
    });
    fixture = TestBed.createComponent(NMateMessageComponent);
  });

  it('renders an answer as markdown with one chip per distinct source', () => {
    const el = render(answer());

    expect(el.querySelector('.nmate-md strong')?.textContent).toBe('Sprint Planning');
    const chips = el.querySelectorAll('.nm-source');
    expect(chips.length).toBe(1);
    expect(chips[0].getAttribute('href')).toBe('/acme/DASH/sprint-planning');
  });

  it('shows the user question as plain text, never as HTML', () => {
    const el = render(answer({ role: 'user', content: '<b>x</b>', citations: [] }));

    expect(el.querySelector('.nm-bubble--user')?.textContent?.trim()).toBe('<b>x</b>');
    expect(el.querySelector('b')).toBeNull();
  });

  it('emits the rating and only offers it on finished answers', () => {
    let rated: number | undefined;
    fixture.componentInstance.rate.subscribe(r => (rated = r));
    const el = render(answer());

    (el.querySelectorAll('.nm-rate__btn')[1] as HTMLButtonElement).click();
    expect(rated).toBe(-1);

    const streaming = render(answer({ status: 'streaming', id: null }));
    expect(streaming.querySelector('.nm-rate')).toBeNull();
  });

  it('shows the typing indicator before the first text and the error line on failure', () => {
    expect(render(answer({ status: 'streaming', content: '', id: null })).querySelector('.nm-typing')).not.toBeNull();

    const failed = render(answer({ status: 'error', errorCode: 'NMATE_QUOTA_EXCEEDED' }));
    expect(failed.querySelector('.nm-error')?.textContent).toContain('hết lượt');
  });
});
