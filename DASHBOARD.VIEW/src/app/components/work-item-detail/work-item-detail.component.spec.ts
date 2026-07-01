import { ComponentFixture, TestBed } from '@angular/core/testing';
import { WorkItemDetailComponent } from './work-item-detail.component';

describe('WorkItemDetailComponent', () => {
  let fixture: ComponentFixture<WorkItemDetailComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WorkItemDetailComponent],
    }).compileComponents();
    fixture = TestBed.createComponent(WorkItemDetailComponent);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });
});
