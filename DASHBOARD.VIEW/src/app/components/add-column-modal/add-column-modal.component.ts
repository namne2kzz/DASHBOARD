import { Component, HostListener, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SPRINT_TASK_STATE_OPTIONS } from '../../core/constants/system.constant';
import type { NewColumnForm, SprintTaskStateKey, WipMode } from '../../models/workflow.model';

@Component({
  selector: 'app-add-column-modal',
  imports: [FormsModule],
  templateUrl: './add-column-modal.component.html',
  styleUrl: './add-column-modal.component.scss',
})
export class AddColumnModalComponent {
  readonly stateOptions = SPRINT_TASK_STATE_OPTIONS;

  readonly saved     = output<NewColumnForm>();
  readonly cancelled = output<void>();

  readonly name        = signal('');
  readonly mappedState = signal<SprintTaskStateKey>('open');
  readonly wipLimit    = signal(0);
  readonly wipMode     = signal<WipMode>('soft');
  readonly agingDays   = signal(5);

  @HostListener('document:keydown.escape')
  onEscape(): void { this.cancelled.emit(); }

  /** Emits the form data if name is valid. */
  submit(): void {
    const name = this.name().trim();
    if (!name) return;
    this.saved.emit({
      name,
      mappedState:    this.mappedState(),
      wipLimit:       this.wipLimit(),
      wipMode:        this.wipMode(),
      agingLimitDays: this.agingDays(),
    });
  }
}
