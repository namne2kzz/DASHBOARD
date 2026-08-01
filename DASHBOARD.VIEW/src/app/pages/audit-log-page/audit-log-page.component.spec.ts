import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { AuditLogPageComponent } from './audit-log-page.component';

describe('AuditLogPageComponent', () => {
  let component: AuditLogPageComponent;
  let fixture: ComponentFixture<AuditLogPageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AuditLogPageComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(AuditLogPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('reports no active filters initially', () => {
    expect(component.hasActiveFilters()).toBe(false);
  });
});
