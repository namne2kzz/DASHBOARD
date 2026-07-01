import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AppTableComponent } from './app-table.component';

describe('AppTableComponent', () => {
  let fixture: ComponentFixture<AppTableComponent<object>>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppTableComponent],
    }).compileComponents();
    fixture = TestBed.createComponent(AppTableComponent<object>);
    fixture.componentRef.setInput('columns', []);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });
});
