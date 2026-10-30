import { TestBed } from '@angular/core/testing';
import { NMateNudgeService } from './nmate-nudge.service';
import { NMateNudge } from '../models/nmate-nudge.model';

describe('NMateNudgeService', () => {
  let service: NMateNudgeService;

  function nudge(key: string): NMateNudge {
    return { key, message: 'Something is off', action: 'Ask NMate', question: 'What happened?' };
  }

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(NMateNudgeService);
  });

  it('starts with nothing on screen', () => {
    expect(service.current()).toBeNull();
  });

  it('raises an offer', () => {
    expect(service.offer(nudge('a'))).toBeTrue();
    expect(service.current()?.key).toBe('a');
  });

  it('refuses to raise the same offer twice in a session', () => {
    service.offer(nudge('a'));
    service.dismiss();

    // Returning to the screen must not raise it again — an assistant that repeats itself
    // is one people learn to dismiss unread.
    expect(service.offer(nudge('a'))).toBeFalse();
    expect(service.current()).toBeNull();
  });

  it('still allows a different problem on the same screen', () => {
    service.offer(nudge('overview:WipExceeded:col-1'));
    service.dismiss();

    expect(service.offer(nudge('overview:Overloaded:user-7'))).toBeTrue();
  });

  it('clears the bubble on dismiss', () => {
    service.offer(nudge('a'));
    service.dismiss();

    expect(service.current()).toBeNull();
  });

  it('lets a withdrawn offer be raised again', () => {
    service.offer(nudge('a'));
    service.withdraw('a');

    expect(service.current()).toBeNull();
    expect(service.offer(nudge('a'))).toBeTrue();
  });

  it('leaves another offer on screen when withdrawing a different key', () => {
    service.offer(nudge('a'));
    service.withdraw('b');

    expect(service.current()?.key).toBe('a');
  });
});
