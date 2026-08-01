import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { WorkItemMetadataEditorComponent } from './work-item-metadata-editor.component';

describe('WorkItemMetadataEditorComponent', () => {
  let component: WorkItemMetadataEditorComponent;
  let fixture: ComponentFixture<WorkItemMetadataEditorComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WorkItemMetadataEditorComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(WorkItemMetadataEditorComponent);
    fixture.componentRef.setInput('taskId', '00000000-0000-0000-0000-000000000000');
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
