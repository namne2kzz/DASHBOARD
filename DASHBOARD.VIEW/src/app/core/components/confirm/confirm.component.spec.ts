import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ConfirmComponent } from './confirm.component';

describe('ConfirmComponent', () => {
  let fixture: ComponentFixture<ConfirmComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ConfirmComponent],
      providers: [
        { provide: 'DIALOG_DATA', useValue: {} },
        { provide: 'DIALOG_REF', useValue: { close: () => {} } },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(ConfirmComponent);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });
});
