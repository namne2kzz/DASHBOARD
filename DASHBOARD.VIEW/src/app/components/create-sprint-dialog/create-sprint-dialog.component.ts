import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
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
  private readonly dialogRef = inject(DIALOG_REF_TOKEN);
  private readonly planning  = inject(SprintPlanningService);

  readonly name      = signal('');
  readonly startDate = signal('');
  readonly endDate   = signal('');

  readonly canSubmit = computed(() =>
    this.name().trim().length > 0 &&
    this.startDate().length > 0 &&
    this.endDate().length > 0 &&
    this.startDate() < this.endDate(),
  );

  /** Creates the sprint and closes the dialog. */
  submit(): void {
    if (!this.canSubmit()) return;
    this.planning.createSprint(this.name().trim(), this.startDate(), this.endDate());
    this.dialogRef.close();
  }

  /** Closes without creating. */
  cancel(): void {
    this.dialogRef.close();
  }
}
