import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { MyWorkPageComponent } from './my-work-page.component';

describe('MyWorkPageComponent', () => {
  let component: MyWorkPageComponent;
  let fixture: ComponentFixture<MyWorkPageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MyWorkPageComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(MyWorkPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('defaults to grouping by sprint', () => {
    expect(component.grouping()).toBe('sprint');
  });

  describe('greeting', () => {
    function at(hour: number): string {
      const clock = new Date();
      clock.setHours(hour, 0, 0, 0);
      jasmine.clock().mockDate(clock);
      return component.greeting();
    }

    beforeEach(() => jasmine.clock().install());
    afterEach(() => jasmine.clock().uninstall());

    it('follows the time of day', () => {
      expect(at(9)).toContain('Good morning');
      expect(at(14)).toContain('Good afternoon');
      expect(at(21)).toContain('Good evening');
    });

    it('greets without a name when there is no signed-in user', () => {
      // Nobody is signed in in this harness, so the greeting must still read as a sentence.
      expect(at(9)).toBe('Good morning');
    });
  });

  describe('summary', () => {
    it('says so plainly when nothing is assigned', () => {
      expect(component.summary()).toBe('Nothing is assigned to you right now.');
    });
  });
});
