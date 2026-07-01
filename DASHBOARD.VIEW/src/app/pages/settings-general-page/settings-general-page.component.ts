import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ThemeService } from '../../core/services/theme.service';

@Component({
  selector: 'app-settings-general-page',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './settings-general-page.component.html',
  styleUrls: ['./settings-general-page.component.css'],
})
export class SettingsGeneralPageComponent {
  readonly themeService = inject(ThemeService);

  readonly workspaceName      = signal('My Workspace');
  readonly description        = signal('');
  readonly emailNotifications = signal(true);
  readonly pushNotifications  = signal(false);

  /** Toggle email notification preference. */
  toggleEmail(): void { this.emailNotifications.update(v => !v); }

  /** Toggle push notification preference. */
  togglePush(): void { this.pushNotifications.update(v => !v); }
}
