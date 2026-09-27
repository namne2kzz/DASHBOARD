import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { HttpCacheService } from './http-cache.service';

/**
 * Unit tests for HttpCacheService.
 *
 * The risk this cache carries is not a missed hit — that only costs a request — but a hit that should
 * have missed. Three cases decide whether it is safe: the factory must not run twice inside the TTL,
 * it must run again once the TTL has passed, and it must run again after an explicit invalidate. If
 * the last two fail the UI silently serves data the user has already changed.
 *
 * Time is advanced by stubbing Date.now rather than by waiting, so a TTL boundary is tested exactly
 * instead of approximately.
 */
describe('HttpCacheService', () => {
  let service: HttpCacheService;
  let now: number;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [HttpCacheService] });
    service = TestBed.inject(HttpCacheService);

    now = 1_000_000;
    spyOn(Date, 'now').and.callFake(() => now);
  });

  /** Counts factory invocations so a test can assert whether a call was served from cache. */
  const countingFactory = () => {
    const spy = jasmine.createSpy('factory').and.callFake(() => of(['value']));
    return spy;
  };

  it('runs the factory once and replays the value on a second call within the TTL', () => {
    const factory = countingFactory();

    service.get('k', 60_000, factory).subscribe();
    service.get('k', 60_000, factory).subscribe();

    expect(factory).toHaveBeenCalledTimes(1);
  });

  it('replays the cached value to a subscriber that arrives after the first completed', () => {
    const factory = countingFactory();
    let received: string[] | undefined;

    service.get<string[]>('k', 60_000, factory).subscribe();
    service.get<string[]>('k', 60_000, factory).subscribe(v => (received = v));

    // refCount:false is what makes this pass — with refCount:true the value would be gone once the
    // first subscriber unsubscribed, and reopening a panel would refetch.
    expect(received).toEqual(['value']);
    expect(factory).toHaveBeenCalledTimes(1);
  });

  it('runs the factory again once the TTL has elapsed', () => {
    const factory = countingFactory();

    service.get('k', 60_000, factory).subscribe();
    now += 60_001;
    service.get('k', 60_000, factory).subscribe();

    expect(factory).toHaveBeenCalledTimes(2);
  });

  it('still serves from cache one millisecond before the TTL expires', () => {
    const factory = countingFactory();

    service.get('k', 60_000, factory).subscribe();
    now += 59_999;
    service.get('k', 60_000, factory).subscribe();

    expect(factory).toHaveBeenCalledTimes(1);
  });

  it('keys entries separately so one key does not serve another', () => {
    const first  = countingFactory();
    const second = countingFactory();

    service.get('a', 60_000, first).subscribe();
    service.get('b', 60_000, second).subscribe();

    expect(first).toHaveBeenCalledTimes(1);
    expect(second).toHaveBeenCalledTimes(1);
  });

  it('refetches after invalidate, even inside the TTL', () => {
    const factory = countingFactory();

    service.get('user-hierarchy:1', 60_000, factory).subscribe();
    service.invalidate('user-hierarchy:');
    service.get('user-hierarchy:1', 60_000, factory).subscribe();

    expect(factory).toHaveBeenCalledTimes(2);
  });

  it('invalidates every key sharing the prefix', () => {
    const one = countingFactory();
    const two = countingFactory();

    service.get('user-hierarchy:1', 60_000, one).subscribe();
    service.get('user-hierarchy:2', 60_000, two).subscribe();
    service.invalidate('user-hierarchy:');
    service.get('user-hierarchy:1', 60_000, one).subscribe();
    service.get('user-hierarchy:2', 60_000, two).subscribe();

    expect(one).toHaveBeenCalledTimes(2);
    expect(two).toHaveBeenCalledTimes(2);
  });

  it('leaves entries outside the prefix untouched', () => {
    const hierarchy = countingFactory();
    const search    = countingFactory();

    service.get('user-hierarchy:1', 60_000, hierarchy).subscribe();
    service.get('user-search:bo', 60_000, search).subscribe();
    service.invalidate('user-hierarchy:');
    service.get('user-search:bo', 60_000, search).subscribe();

    expect(search).toHaveBeenCalledTimes(1);
  });

  it('drops every entry on clear, so a new session starts cold', () => {
    const factory = countingFactory();

    service.get('k', 60_000, factory).subscribe();
    service.clear();
    service.get('k', 60_000, factory).subscribe();

    expect(factory).toHaveBeenCalledTimes(2);
  });
});
