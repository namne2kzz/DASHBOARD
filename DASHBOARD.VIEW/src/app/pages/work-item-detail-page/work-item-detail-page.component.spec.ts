import { ComponentFixture, TestBed } from '@angular/core/testing';
import { WorkItemDetailPageComponent } from './work-item-detail-page.component';

describe('WorkItemDetailPageComponent', () => {
  let component: WorkItemDetailPageComponent;
  let fixture: ComponentFixture<WorkItemDetailPageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WorkItemDetailPageComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(WorkItemDetailPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
