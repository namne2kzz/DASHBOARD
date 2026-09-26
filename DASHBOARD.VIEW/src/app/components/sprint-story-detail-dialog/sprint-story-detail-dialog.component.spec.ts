import { ComponentFixture, TestBed } from '@angular/core/testing';
import { InjectionToken } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideToastr } from 'ngx-toastr';
import { SprintStoryDetailDialogComponent } from './sprint-story-detail-dialog.component';
import { DIALOG_REF_TOKEN } from '../../models/dialog.model';
import { SprintStoryDetailDialogData } from '../../models/sprint-planning.model';

const dialogData = {
  story:    { id: 'story-1', title: 'Story', type: 'user-story' },
  subTasks: [],
} as unknown as SprintStoryDetailDialogData;

describe('SprintStoryDetailDialogComponent', () => {
  let fixture: ComponentFixture<SprintStoryDetailDialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SprintStoryDetailDialogComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]), provideNoopAnimations(), provideToastr(),
        { provide: DIALOG_REF_TOKEN, useValue: { close: jasmine.createSpy('close') } },
        // The component injects the data token by string identity, so mirror that here.
        { provide: 'DIALOG_DATA' as unknown as InjectionToken<SprintStoryDetailDialogData>, useValue: dialogData },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(SprintStoryDetailDialogComponent);
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });
});
