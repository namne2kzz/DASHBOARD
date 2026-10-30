import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MetricDeltaComponent } from './metric-delta.component';

describe('MetricDeltaComponent', () => {
  let fixture: ComponentFixture<MetricDeltaComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [MetricDeltaComponent] }).compileComponents();
    fixture = TestBed.createComponent(MetricDeltaComponent);
  });

  function render(delta: number | null, lowerIsBetter = false): HTMLElement | null {
    fixture.componentRef.setInput('delta', delta);
    fixture.componentRef.setInput('lowerIsBetter', lowerIsBetter);
    fixture.detectChanges();
    return fixture.nativeElement.querySelector('.nx-delta');
  }

  it('renders nothing without a previous period to compare against', () => {
    expect(render(null)).toBeNull();
  });

  it('renders nothing when the figure did not move', () => {
    expect(render(0)).toBeNull();
  });

  it('shows a rise as favourable by default', () => {
    const el = render(3);
    expect(el?.classList).toContain('nx-delta--up');
    expect(el?.textContent).toContain('↑');
    expect(el?.textContent).toContain('3');
  });

  it('shows a fall as unfavourable by default', () => {
    expect(render(-2)?.classList).toContain('nx-delta--down');
  });

  it('inverts the judgement where lower is better, such as cycle time', () => {
    // The same fall that is bad news for throughput is good news for cycle time.
    expect(render(-0.3, true)?.classList).toContain('nx-delta--up');
    expect(render(0.4, true)?.classList).toContain('nx-delta--down');
  });

  it('states the direction in words for screen readers', () => {
    expect(render(5)?.querySelector('.sr-only')?.textContent).toBe('up');
    expect(render(-5)?.querySelector('.sr-only')?.textContent).toBe('down');
  });

  it('prints the magnitude without its sign, since the arrow carries direction', () => {
    expect(render(-7)?.textContent).toContain('7');
    expect(render(-7)?.textContent).not.toContain('-7');
  });
});
