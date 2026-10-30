import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NBotComponent } from './n-bot.component';
import { NBotMood } from '../../models/n-bot.model';

describe('NBotComponent', () => {
  let fixture: ComponentFixture<NBotComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [NBotComponent] }).compileComponents();
    fixture = TestBed.createComponent(NBotComponent);
  });

  function render(mood?: NBotMood): SVGElement {
    if (mood) fixture.componentRef.setInput('mood', mood);
    fixture.detectChanges();
    return fixture.nativeElement.querySelector('svg');
  }

  it('wears the open-eyed face by default', () => {
    const svg = render();
    expect(svg.querySelector('.nb-eye--left')).not.toBeNull();
    expect(svg.querySelector('.nb-stroke')).not.toBeNull();
  });

  it('keeps the hexagon head in every mood', () => {
    const moods: NBotMood[] = ['normal', 'happy', 'sleeping', 'maintenance', 'error'];
    for (const mood of moods) {
      expect(render(mood).querySelector('.nb-head')).withContext(mood).not.toBeNull();
    }
  });

  it('swaps the antenna for a hard hat while under maintenance', () => {
    const svg = render('maintenance');
    expect(svg.querySelector('.nb-antenna')).toBeNull();
    expect(svg.querySelector('.nb-hat')).not.toBeNull();
  });

  it('greys the antenna tip when asleep and reddens it on error', () => {
    expect(render('sleeping').querySelector('circle')?.getAttribute('fill')).toBe('#8B949E');

    fixture = TestBed.createComponent(NBotComponent);
    expect(render('error').querySelector('circle')?.getAttribute('fill')).toBe('#CF222E');
  });

  it('is hidden from screen readers unless given a label', () => {
    expect(render().getAttribute('aria-hidden')).toBe('true');

    fixture.componentRef.setInput('label', 'All caught up');
    fixture.detectChanges();
    const svg: SVGElement = fixture.nativeElement.querySelector('svg');
    expect(svg.getAttribute('aria-hidden')).toBeNull();
    expect(svg.getAttribute('aria-label')).toBe('All caught up');
    expect(svg.getAttribute('role')).toBe('img');
  });

  it('renders at the requested size', () => {
    fixture.componentRef.setInput('size', 120);
    expect(render().getAttribute('width')).toBe('120');
  });
});
