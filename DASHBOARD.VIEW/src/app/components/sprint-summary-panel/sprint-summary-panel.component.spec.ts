import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SprintSummaryPanelComponent } from './sprint-summary-panel.component';
import { SprintSummaryService } from '../../services/sprint-summary.service';
import { signal } from '@angular/core';

describe('SprintSummaryPanelComponent', () => {
  let fixture: ComponentFixture<SprintSummaryPanelComponent>;
  let svcSpy: jasmine.SpyObj<SprintSummaryService>;

  beforeEach(async () => {
    svcSpy = jasmine.createSpyObj('SprintSummaryService', ['formatDate'], {
      loading: signal(false),
      error: signal(null),
      activeSprint: signal(null),
      summary: signal(null),
      sprintStatus: signal('completed'),
      daysRemaining: signal(null),
      storyPointPercent: signal(0),
      taskPercent: signal(0),
    });

    await TestBed.configureTestingModule({
      imports: [SprintSummaryPanelComponent],
      providers: [{ provide: SprintSummaryService, useValue: svcSpy }],
    }).compileComponents();

    fixture = TestBed.createComponent(SprintSummaryPanelComponent);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should show loading spinner when loading', () => {
    (svcSpy.loading as ReturnType<typeof signal>).set(true);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Loading sprint info');
  });

  it('should show empty state when no active sprint', () => {
    expect(fixture.nativeElement.textContent).toContain('No active sprint');
  });
});
