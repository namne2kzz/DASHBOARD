import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideToastr } from 'ngx-toastr';
import { WorkItemDetailPageComponent } from './work-item-detail-page.component';

describe('WorkItemDetailPageComponent', () => {
  let component: WorkItemDetailPageComponent;
  let fixture: ComponentFixture<WorkItemDetailPageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WorkItemDetailPageComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]), provideNoopAnimations(), provideToastr()],
    }).compileComponents();

    fixture = TestBed.createComponent(WorkItemDetailPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
