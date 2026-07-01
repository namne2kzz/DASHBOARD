import { ChangeDetectionStrategy, Component, computed, inject, InjectionToken, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DIALOG_REF_TOKEN } from '../../models/dialog.model';
import { SprintPlanningService } from '../../services/sprint-planning.service';
import { AddSprintTaskDialogData } from '../../models/sprint-planning.model';

@Component({
  selector: 'app-add-sprint-task-dialog',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './add-sprint-task-dialog.component.html',
  styleUrl: './add-sprint-task-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AddSprintTaskDialogComponent {
  private readonly dialogRef = inject(DIALOG_REF_TOKEN);
  readonly planning          = inject(SprintPlanningService);
  readonly data              = inject('DIALOG_DATA' as unknown as InjectionToken<AddSprintTaskDialogData>);

  readonly title            = signal('');
  readonly description      = signal('');
  readonly priority         = signal(1); // Medium
  readonly originalEstimate = signal(0);
  readonly assignedToId     = signal<string | null>(null);

  readonly priorities = [
    { value: 0, label: 'Low' },
    { value: 1, label: 'Medium' },
    { value: 2, label: 'High' },
    { value: 3, label: 'Critical' },
  ];

  readonly estimateError = computed(() => {
    const v = this.originalEstimate();
    if (isNaN(v) || v <= 0) return 'Must be a number greater than 0.';
    return null;
  });

  readonly canSubmit = computed(() =>
    this.title().trim().length > 0 && this.estimateError() === null,
  );

  /** Submits the new task and closes the dialog. */
  submit(): void {
    if (!this.canSubmit()) return;
    this.planning.createSubTask(
      this.data.storyId,
      this.title(),
      this.description(),
      this.priority(),
      this.originalEstimate(),
      this.assignedToId(),
    );
    this.dialogRef.close();
  }

  /** Closes without creating. */
  cancel(): void {
    this.dialogRef.close();
  }
}
