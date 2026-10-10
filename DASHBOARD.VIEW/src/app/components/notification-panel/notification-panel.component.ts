import { Component, inject, input, output } from '@angular/core';
import { Router } from '@angular/router';
import { NotificationService } from '../../services/notification.service';
import { AppNotification } from '../../models/notification.model';

/** Full-height slide-in notification panel rendered in the shell. */
@Component({
  selector: 'app-notification-panel',
  imports: [],
  templateUrl: './notification-panel.component.html',
  styleUrl: './notification-panel.component.scss',
})
export class NotificationPanelComponent {
  readonly notiSvc = inject(NotificationService);
  private readonly router = inject(Router);

  readonly visible = input(false);
  readonly closed  = output<void>();

  close(): void { this.closed.emit(); }

  /** @param n Notification to open — navigates to its URL and marks it read. */
  async openNotification(n: AppNotification): Promise<void> {
    this.close();
    await this.notiSvc.open(n);
  }

  async markAllRead(): Promise<void> {
    await this.notiSvc.markAllRead();
  }

  /** @returns Human-friendly relative time. @param iso ISO date string. */
  relativeTime(iso: string): string {
    const diff = Date.now() - new Date(iso).getTime();
    const m = Math.floor(diff / 60_000);
    if (m < 1)  return 'just now';
    if (m < 60) return `${m}m ago`;
    const h = Math.floor(m / 60);
    if (h < 24) return `${h}h ago`;
    return `${Math.floor(h / 24)}d ago`;
  }

  /** @returns SVG path for the notification type icon. @param type Notification type. */
  iconPath(type: AppNotification['type']): string {
    switch (type) {
      case 'WorkItemAssigned':      return 'M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M12 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM19 8l2 2 4-4';
      case 'WorkItemUnassigned':    return 'M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M12 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM23 11l-4 4';
      case 'WorkItemStateChanged':  return 'M9 11l3 3L22 4M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11';
      case 'SprintActivated':       return 'M13 10V3L4 14h7v7l9-11h-7z';
      case 'SprintClosed':          return 'M9 12l2 2 4-4m6 2a9 9 0 1 1-18 0 9 9 0 0 1 18 0z';
      case 'MentionedInDiscussion': return 'M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z';
      default: return 'M15 17h5l-1.405-1.405A2.032 2.032 0 0 1 18 14.158V11a6 6 0 0 0-10-5.659V5a2 2 0 0 0-4 0v.341C5.67 6.165 4 8.388 4 11v3.159c0 .538-.214 1.055-.595 1.436L2 17h5m6 0v1a3 3 0 1 1-6 0v-1m6 0H9';
    }
  }

  /** @returns Tailwind color class for the notification type icon. @param type Notification type. */
  iconColor(type: AppNotification['type']): string {
    switch (type) {
      case 'WorkItemAssigned':      return 'text-sky-400';
      case 'WorkItemUnassigned':    return 'text-slate-400';
      case 'WorkItemStateChanged':  return 'text-violet-400';
      case 'SprintActivated':       return 'text-emerald-400';
      case 'SprintClosed':          return 'text-rose-400';
      case 'MentionedInDiscussion': return 'text-amber-400';
      default:                      return 'text-slate-400';
    }
  }
}
