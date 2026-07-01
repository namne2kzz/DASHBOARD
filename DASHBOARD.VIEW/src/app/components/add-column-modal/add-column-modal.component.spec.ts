import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AddColumnModalComponent } from './add-column-modal.component';

describe('AddColumnModalComponent', () => {
  let component: AddColumnModalComponent;
  let fixture: ComponentFixture<AddColumnModalComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AddColumnModalComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(AddColumnModalComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
