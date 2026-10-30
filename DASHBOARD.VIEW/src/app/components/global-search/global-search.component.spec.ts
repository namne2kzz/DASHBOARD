import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { GlobalSearchComponent } from './global-search.component';

describe('GlobalSearchComponent', () => {
  let component: GlobalSearchComponent;
  let fixture: ComponentFixture<GlobalSearchComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [GlobalSearchComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(GlobalSearchComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('is closed by default', () => {
    expect(component.search.open()).toBe(false);
  });

  describe('commands', () => {
    it('lists everything it can do when nothing has been typed', () => {
      // This is how someone discovers the palette does more than search.
      expect(component.commands().length).toBeGreaterThan(0);
    });

    it('matches a command by its label', () => {
      component.search.setTerm('sprint');
      fixture.detectChanges();

      expect(component.commands().map(c => c.id)).toContain('new-sprint');
    });

    it('matches a command by what people actually call it', () => {
      // "Switch to dark mode" never says "theme", but that is the word people reach for.
      component.search.setTerm('theme');
      fixture.detectChanges();

      expect(component.commands().map(c => c.id)).toContain('toggle-theme');
    });

    it('names the theme command after what it will do, not the current state', () => {
      const label = component.commands().find(c => c.id === 'toggle-theme')?.label;
      expect(label).toMatch(/Switch to (light|dark) mode/);
    });

    it('drops commands that match nothing', () => {
      component.search.setTerm('zzzzz');
      fixture.detectChanges();

      expect(component.commands()).toEqual([]);
    });
  });

  describe('asking NMate', () => {
    it('stays hidden until there is enough text to be a question', () => {
      expect(component.canAskNMate()).toBeFalse();

      component.search.setTerm('a');
      fixture.detectChanges();
      expect(component.canAskNMate()).toBeFalse();

      component.search.setTerm('why is this sprint behind');
      fixture.detectChanges();
      expect(component.canAskNMate()).toBeTrue();
    });
  });
});
