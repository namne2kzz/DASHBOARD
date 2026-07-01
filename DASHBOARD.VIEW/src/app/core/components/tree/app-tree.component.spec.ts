import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AppTreeComponent } from './app-tree.component';

describe('AppTreeComponent', () => {
  let fixture: ComponentFixture<AppTreeComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppTreeComponent],
    }).compileComponents();
    fixture = TestBed.createComponent(AppTreeComponent);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });
});
