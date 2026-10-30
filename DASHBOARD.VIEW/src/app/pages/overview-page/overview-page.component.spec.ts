import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { OverviewPageComponent } from './overview-page.component';
import { OverviewService } from '../../services/overview.service';
import type { AttentionItemDto, OverviewStatsDto } from '../../models/overview.model';

/**
 * Unit tests for the overview page.
 *
 * Focused on the attention strip's severity, which decides how loudly the page shouts. A strip
 * that is always red stops being read, so the rule that picks its colour is worth pinning.
 */
describe('OverviewPageComponent', () => {
  /** Minimal stats payload: only the attention list matters here. */
  function statsWith(attention: AttentionItemDto[]): OverviewStatsDto {
    return { attention } as unknown as OverviewStatsDto;
  }

  /** Builds an attention item of the given severity. */
  function item(severity: 'Warning' | 'Critical'): AttentionItemDto {
    return {
      kind: 'Wip',
      severity,
      message: `${severity} item`,
      route: 'boards',
      targetId: null,
    } as unknown as AttentionItemDto;
  }

  /**
   * Creates the component over a stubbed OverviewService.
   * @param attention The attention items the server is pretending to return.
   */
  function createWith(attention: AttentionItemDto[]) {
    const stats = signal<OverviewStatsDto | null>(statsWith(attention));

    TestBed.configureTestingModule({
      imports: [OverviewPageComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: OverviewService, useValue: { stats, loading: signal(false), error: signal(null) } },
      ],
    });

    return TestBed.createComponent(OverviewPageComponent).componentInstance;
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('attentionLevel', () => {
    it('stays amber when every item is a warning', () => {
      // A card going stale is worth a look, not an alarm.
      const component = createWith([item('Warning'), item('Warning')]);

      expect(component.attentionLevel()).toBe('warning');
    });

    it('turns red as soon as one item is critical', () => {
      // One genuine breach sets the tone for the whole strip — the reader needs to see that the
      // board is in a different state, not hunt for which chip is the bad one.
      const component = createWith([item('Warning'), item('Critical')]);

      expect(component.attentionLevel()).toBe('critical');
    });

    it('is red regardless of where the critical item sits in the list', () => {
      const component = createWith([item('Critical'), item('Warning')]);

      expect(component.attentionLevel()).toBe('critical');
    });

    it('reports amber for an empty list, which the template never renders anyway', () => {
      // The strip is hidden entirely when there is nothing to show, so this only guards against
      // the computed throwing on an empty array.
      const component = createWith([]);

      expect(component.attentionLevel()).toBe('warning');
      expect(component.attention().length).toBe(0);
    });
  });
});
