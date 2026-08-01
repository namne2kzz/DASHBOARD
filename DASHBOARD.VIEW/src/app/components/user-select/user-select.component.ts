import { Component, ElementRef, HostListener, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgClass } from '@angular/common';

/** A selectable user option, normalised across the app's various user shapes. */
export interface UserOption {
  id: string;
  name: string;
  email?: string | null;
  avatarClass: string;
}

/**
 * A searchable user picker matching the app's member-picker look: a trigger showing the selected
 * user's avatar + name, and a dropdown with a search box and avatar rows. Filters an in-memory list.
 */
@Component({
  selector: 'app-user-select',
  imports: [FormsModule, NgClass],
  templateUrl: './user-select.component.html',
  styleUrl: './user-select.component.css',
})
export class UserSelectComponent {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  /** Candidate users to choose from. */
  readonly users = input<UserOption[]>([]);
  /** Currently-selected user id, or null. */
  readonly selectedId = input<string | null>(null);
  /** Placeholder shown when nothing is selected. */
  readonly placeholder = input('Select a user');
  /** Whether a "clear" (none) option is offered. */
  readonly allowClear = input(true);
  /** Label for the clear option. */
  readonly clearLabel = input('— None —');

  /** Emits the newly-selected user id, or null when cleared. */
  readonly selected = output<string | null>();

  readonly open   = signal(false);
  readonly search = signal('');

  /** The currently-selected option, resolved from the list. */
  readonly selectedUser = computed(() => this.users().find(u => u.id === this.selectedId()) ?? null);

  /** Users matching the search term. */
  readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    if (!q) return this.users();
    return this.users().filter(u =>
      u.name.toLowerCase().includes(q) || (u.email ?? '').toLowerCase().includes(q));
  });

  /** Closes the dropdown when clicking anywhere outside this component. */
  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.open() && !this.host.nativeElement.contains(event.target as Node)) this.close();
  }

  /** Toggles the dropdown open/closed, resetting the search on open. */
  toggle(): void {
    this.search.set('');
    this.open.update(v => !v);
  }

  /** Closes the dropdown. */
  close(): void {
    this.open.set(false);
  }

  /** Selects a user (or clears) and closes. @param id The chosen user id, or null to clear. */
  pick(id: string | null): void {
    this.selected.emit(id);
    this.close();
  }

  /** Two-letter initials for an avatar chip. @param name Full name. */
  initials(name: string): string {
    const parts = name.trim().split(/\s+/).filter(Boolean);
    if (!parts.length) return '?';
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  }
}
