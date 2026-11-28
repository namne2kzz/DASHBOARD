import { DatePipe } from '@angular/common';
import { Component, computed, HostListener, inject, OnInit, output } from '@angular/core';
import { AboutService } from '../../services/about.service';
import type { ServiceStatusDto } from '../../models/about.model';

/** Shows what this build is and which services it is talking to, with live status. */
@Component({
  selector: 'app-about-dialog',
  imports: [DatePipe],
  templateUrl: './about-dialog.component.html',
  styleUrl: './about-dialog.component.scss',
})
export class AboutDialogComponent implements OnInit {
  private readonly about = inject(AboutService);

  readonly closed = output<void>();

  readonly loading = this.about.loading;
  readonly error   = this.about.error;
  readonly info    = this.about.info;

  /** The frontend's own build-time environment, which the API cannot report. */
  readonly client = computed(() => ({
    url:      window.location.origin,
    language: navigator.language,
    timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone,
  }));

  /** Probes every dependency when the dialog opens — never before. */
  ngOnInit(): void {
    this.about.load();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void { this.closed.emit(); }

  /** Re-runs the probes. @returns Nothing. */
  refresh(): void { this.about.load(); }

  /**
   * Dot colour for a service row.
   * @param status The reported status.
   * @returns A CSS custom property reference for the dot background.
   */
  statusColor(status: ServiceStatusDto['status']): string {
    switch (status) {
      case 'healthy':     return 'var(--success)';
      case 'unreachable': return 'var(--danger)';
      // Both "configured" and "not-configured" are grey: neither is a fault, so neither
      // earns a colour that pulls the eye the way a real failure should.
      default:            return 'var(--text-3)';
    }
  }

  /**
   * Human-readable label for a status.
   * @param status The reported status.
   * @returns Short word shown next to the dot.
   */
  statusLabel(status: ServiceStatusDto['status']): string {
    switch (status) {
      case 'healthy':         return 'Connected';
      case 'unreachable':     return 'Unreachable';
      case 'not-configured':  return 'Not configured';
      default:                return 'Configured';
    }
  }
}
