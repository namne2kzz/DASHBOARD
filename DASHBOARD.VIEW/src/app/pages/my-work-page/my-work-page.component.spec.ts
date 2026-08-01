import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { MyWorkPageComponent } from './my-work-page.component';

describe('MyWorkPageComponent', () => {
  let component: MyWorkPageComponent;
  let fixture: ComponentFixture<MyWorkPageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MyWorkPageComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(MyWorkPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('defaults to grouping by sprint', () => {
    expect(component.grouping()).toBe('sprint');
  });
});
