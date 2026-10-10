import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NotificationPanelComponent } from './notification-panel.component';

describe('NotificationPanelComponent', () => {
  let fixture: ComponentFixture<NotificationPanelComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [NotificationPanelComponent] }).compileComponents();
    fixture = TestBed.createComponent(NotificationPanelComponent);
    fixture.detectChanges();
  });

  it('should create', () => expect(fixture.componentInstance).toBeTruthy());
});
