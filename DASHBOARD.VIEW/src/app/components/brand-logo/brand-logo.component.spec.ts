import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BrandLogoComponent } from './brand-logo.component';

describe('BrandLogoComponent', () => {
  let fixture: ComponentFixture<BrandLogoComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [BrandLogoComponent] }).compileComponents();
    fixture = TestBed.createComponent(BrandLogoComponent);
  });

  function svg(): SVGElement {
    return fixture.nativeElement.querySelector('svg');
  }

  it('renders at the requested size', () => {
    fixture.componentRef.setInput('size', 40);
    fixture.detectChanges();

    expect(svg().getAttribute('width')).toBe('40');
    expect(svg().getAttribute('height')).toBe('40');
  });

  it('draws the hexagon only, without the N-bot antenna', () => {
    fixture.detectChanges();

    // The antenna is a <line>; the mark is a hexagon, two eyes and the N stroke.
    expect(svg().querySelector('line')).toBeNull();
    expect(svg().querySelectorAll('polygon').length).toBe(2);
    expect(svg().querySelectorAll('rect').length).toBe(2);
  });

  it('takes the dark-surface rim only when asked', () => {
    fixture.detectChanges();
    expect(svg().classList).not.toContain('nx-logo--on-dark');

    fixture.componentRef.setInput('onDark', true);
    fixture.detectChanges();
    expect(svg().classList).toContain('nx-logo--on-dark');
  });

  it('is hidden from screen readers, since a product name always sits beside it', () => {
    fixture.detectChanges();
    expect(svg().getAttribute('aria-hidden')).toBe('true');
  });
});
