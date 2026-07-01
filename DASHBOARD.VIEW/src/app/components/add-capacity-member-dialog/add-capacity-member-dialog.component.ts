import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgClass } from '@angular/common';
import { DIALOG_REF_TOKEN } from '../../models/dialog.model';
import { SprintPlanningService } from '../../services/sprint-planning.service';
import { MembersService } from '../../services/members.service';
import type { MemberApiDto } from '../../models/member.model';

@Component({
  selector: 'app-add-capacity-member-dialog',
  standalone: true,
  imports: [FormsModule, NgClass],
  templateUrl: './add-capacity-member-dialog.component.html',
  styleUrl: './add-capacity-member-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AddCapacityMemberDialogComponent {
  private readonly dialogRef = inject(DIALOG_REF_TOKEN);
  private readonly planning  = inject(SprintPlanningService);
  readonly members           = inject(MembersService);

  readonly search       = signal('');
  readonly dropdownRect = signal<{ top: number; left: number; width: number } | null>(null);
  readonly selectedUserId = signal<string | null>(null);
  readonly role           = signal<string>('');
  readonly hoursPerDay    = signal(8);
  readonly overtime       = signal(0);

  readonly availableMembers = computed(() => {
    const existing = new Set(this.planning.capacityMembers().map(m => m.userId));
    return this.members.members().filter(m => !existing.has(m.userId));
  });

  readonly filteredMembers = computed(() => {
    const q = this.search().toLowerCase();
    if (!q) return this.availableMembers();
    return this.availableMembers().filter(m =>
      m.userName.toLowerCase().includes(q) || m.userEmail.toLowerCase().includes(q),
    );
  });

  readonly selectedMember = computed(() =>
    this.members.members().find(m => m.userId === this.selectedUserId()) ?? null,
  );

  readonly hoursError = computed(() => {
    const v = this.hoursPerDay();
    if (isNaN(v) || v <= 0) return 'Must be greater than 0.';
    if (v > 12)            return 'Maximum is 12h/day.';
    return null;
  });

  readonly overtimeError = computed(() => {
    const v = this.overtime();
    if (isNaN(v) || v < 0) return 'Cannot be negative.';
    if (v > 6)             return 'Maximum is 6h/day.';
    return null;
  });

  readonly roleLabel = computed(() => this.role() || '—');

  readonly canSubmit = computed(() =>
    !!this.selectedUserId() && !this.hoursError() && !this.overtimeError(),
  );

  /** Updates search filter and positions the dropdown below the input. @param term Search text. @param el Input element for positioning. */
  onSearch(term: string, el: HTMLInputElement): void {
    this.search.set(term);
    if (term.trim()) {
      const r = el.getBoundingClientRect();
      this.dropdownRect.set({ top: r.bottom + 4, left: r.left, width: r.width });
    } else {
      this.dropdownRect.set(null);
    }
  }

  /** Selects a member, pre-fills role, and closes the dropdown. @param m The selected member. */
  select(m: MemberApiDto): void {
    this.selectedUserId.set(m.userId);
    this.role.set(m.defaultRole);
    this.search.set('');
    this.dropdownRect.set(null);
  }

  /** Clears the selected member. */
  clearSelection(): void {
    this.selectedUserId.set(null);
    this.search.set('');
    this.dropdownRect.set(null);
  }

  /** Submits the new capacity member and closes. */
  submit(): void {
    const userId = this.selectedUserId();
    if (!userId || !this.canSubmit()) return;
    this.planning.addCapacityMember(userId, this.role(), this.hoursPerDay(), this.overtime());
    this.dialogRef.close();
  }

  /** Closes without saving. */
  cancel(): void {
    this.dialogRef.close();
  }
}
