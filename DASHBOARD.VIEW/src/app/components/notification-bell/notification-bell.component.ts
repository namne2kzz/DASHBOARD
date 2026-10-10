import { Component, inject, input, output } from '@angular/core';
import { NotificationService } from '../../services/notification.service';

/** Nav-item bell button. Emits toggle so the parent shell controls the panel open state. */
@Component({
  selector: 'app-notification-bell',
  imports: [],
  templateUrl: './notification-bell.component.html',
  styleUrl: './notification-bell.component.scss',
})
export class NotificationBellComponent {
  readonly notiSvc   = inject(NotificationService);
  readonly collapsed = input(false);
  /** Whether the panel is currently open (drives is-active state). */
  readonly open      = input(false);
  /** Fired when the user clicks the bell button. */
  readonly toggle    = output<void>();
}
