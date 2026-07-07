import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { DIALOG_REF_TOKEN } from '../../models/dialog.model';
import { SprintPlanningService } from '../../services/sprint-planning.service';

@Component({
  selector: 'app-create-sprint-dialog',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './create-sprint-dialog.component.html',
  styleUrl: './create-sprint-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CreateSprintDialogComponent {
  private readonly dialogRef  = inject(DIALOG_REF_TOKEN);
  private readonly planning   = inject(SprintPlanningService);
  private readonly destroyRef = inject(DestroyRef);

  readonly name        = signal('');
  readonly startDate   = signal('');
  readonly endDate     = signal('');
  readonly submitting  = signal(false);
  readonly submitError = signal<string | null>(null);

  readonly canSubmit = computed(() =>
    !this.submitting() &&
    this.name().trim().length > 0 &&
    this.startDate().length > 0 &&
    this.endDate().length > 0 &&
    this.startDate() < this.endDate(),
  );

  /** Creates the sprint, closes dialog on success or shows inline error on failure. */
  submit(): void {
    if (!this.canSubmit()) return;
    this.submitting.set(true);
    this.submitError.set(null);
    this.planning
      .createSprint(this.name().trim(), this.startDate(), this.endDate())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next:  () => this.dialogRef.close(),
        error: (err) => {
          this.submitting.set(false);
          this.submitError.set(
            err?.error?.error
              ?? Object.values<string[]>(err?.error?.errors ?? {})?.[0]?.[0]
              ?? 'Failed to create sprint.',
          );
        },
      });
  }

  /** Closes without creating. */
  cancel(): void {
    this.dialogRef.close();
  }
}
