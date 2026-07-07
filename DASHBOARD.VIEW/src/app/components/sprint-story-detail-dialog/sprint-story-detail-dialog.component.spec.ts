import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SprintStoryDetailDialogComponent } from './sprint-story-detail-dialog.component';

describe('SprintStoryDetailDialogComponent', () => {
  let fixture: ComponentFixture<SprintStoryDetailDialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SprintStoryDetailDialogComponent],
    }).compileComponents();
    fixture = TestBed.createComponent(SprintStoryDetailDialogComponent);
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });
});
