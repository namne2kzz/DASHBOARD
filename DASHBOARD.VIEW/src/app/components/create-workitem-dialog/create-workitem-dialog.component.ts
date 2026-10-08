import { ChangeDetectionStrategy, Component, computed, inject, InjectionToken, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DIALOG_REF_TOKEN } from '../../models/dialog.model';
import { CreateWorkItemDialogData } from '../../models/sprint-planning.model';
import { SprintBoardService } from '../../services/sprint-board.service';
import { SprintTaskApiType, WorkItemApiPriority } from '../../core/enums/system.enum';
import type { WorkItemPickerApiDto } from '../../models/sprint-planning-api.model';

const TYPE_ICON_CLASS: Record<SprintTaskApiType, string> = {
  [SprintTaskApiType.UserStory]: 'text-type-story ring-slate-800 bg-slate-800',
  [SprintTaskApiType.Task]:      'text-type-task ring-slate-800 bg-slate-800',
  [SprintTaskApiType.Bug]:       'text-type-bug ring-slate-800 bg-slate-800',
  [SprintTaskApiType.TestPlan]:  'text-type-test ring-slate-800 bg-slate-800',
};

@Component({
  selector: 'app-create-workitem-dialog',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './create-workitem-dialog.component.html',
  styleUrl: './create-workitem-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CreateWorkItemDialogComponent {
  private readonly dialogRef = inject(DIALOG_REF_TOKEN);
  readonly board             = inject(SprintBoardService);
  readonly data              = inject('DIALOG_DATA' as unknown as InjectionToken<CreateWorkItemDialogData>);

  /** Exposed so the template @switch can compare against enum members. */
  readonly sprintTaskApiType = SprintTaskApiType;

  readonly type          = computed(() => this.data.type);
  readonly typeIconClass = computed(() => TYPE_ICON_CLASS[this.type()]);

  readonly isBug      = computed(() => this.type() === SprintTaskApiType.Bug);
  readonly isTask     = computed(() => this.type() === SprintTaskApiType.Task);
  readonly isTestPlan = computed(() => this.type() === SprintTaskApiType.TestPlan);

  /** Task and Bug share unit-test / design-review notes; TestPlan and UserStory don't use them. */
  readonly showDevNotes = computed(() => this.isBug() || this.isTask());
  /** Original estimate applies to Task and Bug only — TestPlan tracks steps instead of hours. */
  readonly showEstimate = computed(() => !this.isTestPlan());

  // ── Common fields ─────────────────────────────────────────────────────────
  readonly title            = signal('');
  readonly description      = signal('');
  readonly priority         = signal(WorkItemApiPriority.Medium);
  readonly originalEstimate = signal(0);
  readonly assignedToId     = signal<string | null>(null);

  // ── Parent (User Story) picker ───────────────────────────────────────────
  readonly parentSearchTerm  = signal('');
  readonly parentResults     = signal<WorkItemPickerApiDto[]>([]);
  readonly parentSearching   = signal(false);
  readonly parentDropdownRect = signal<{ top: number; left: number; width: number } | null>(null);
  readonly selectedParent    = signal<WorkItemPickerApiDto | null>(null);

  private readonly parentSearch$ = new Subject<string>();

  // ── Bug-specific fields ──────────────────────────────────────────────────
  readonly stepsToReproduce = signal('');
  readonly environment      = signal('');
  readonly rootCause        = signal('');
  readonly solution         = signal('');
  readonly impaction        = signal('');

  // ── Task + Bug shared fields ─────────────────────────────────────────────
  readonly unitTest     = signal('');
  readonly designReview = signal('');

  // ── TestPlan-specific fields ─────────────────────────────────────────────
  readonly testSteps = signal<string[]>(['']);
  readonly automated = signal(false);

  readonly priorities = [
    { value: WorkItemApiPriority.Low,      label: 'Low' },
    { value: WorkItemApiPriority.Medium,   label: 'Medium' },
    { value: WorkItemApiPriority.High,     label: 'High' },
    { value: WorkItemApiPriority.Critical, label: 'Critical' },
  ];

  readonly estimateError = computed(() => {
    if (!this.showEstimate()) return null;
    const v = this.originalEstimate();
    if (isNaN(v) || v < 0) return 'Must be 0 or greater.';
    return null;
  });

  readonly canSubmit = computed(() =>
    this.title().trim().length > 0 && this.estimateError() === null,
  );

  constructor() {
    this.parentSearch$.pipe(
      debounceTime(500),
      distinctUntilChanged(),
      switchMap(term => {
        if (term.length < 2) {
          this.parentResults.set([]);
          this.parentSearching.set(false);
          return [];
        }
        return this.board.searchParentStories(term);
      }),
      takeUntilDestroyed(),
    ).subscribe({
      next:  results => { this.parentResults.set(results); this.parentSearching.set(false); },
      error: ()      => { this.parentResults.set([]); this.parentSearching.set(false); },
    });
  }

  /**
   * Feeds a new search term into the debounced parent-story search and positions the floating dropdown.
   * @param term Raw input value.
   * @param el The input element from the DOM event.
   */
  onParentSearch(term: string, el: HTMLInputElement): void {
    this.parentSearchTerm.set(term);
    const trimmed = term.trim();
    if (trimmed.length >= 2) {
      this.parentSearching.set(true);
      const r = el.getBoundingClientRect();
      this.parentDropdownRect.set({ top: r.bottom + 4, left: r.left, width: r.width });
    } else {
      this.parentResults.set([]);
      this.parentSearching.set(false);
      this.parentDropdownRect.set(null);
    }
    this.parentSearch$.next(trimmed);
  }

  /** Selects a User Story as the parent and closes the dropdown. @param item The picked story. */
  selectParent(item: WorkItemPickerApiDto): void {
    this.selectedParent.set(item);
    this.parentResults.set([]);
    this.parentSearchTerm.set('');
    this.parentDropdownRect.set(null);
  }

  /** Clears the selected parent so a new search can be started. */
  clearParent(): void {
    this.selectedParent.set(null);
    this.parentSearchTerm.set('');
  }

  /** Appends a blank test step row. */
  addTestStep(): void {
    this.testSteps.update(steps => [...steps, '']);
  }

  /** Removes the test step at the given index. @param index Row to remove. */
  removeTestStep(index: number): void {
    this.testSteps.update(steps => steps.filter((_, i) => i !== index));
  }

  /** Updates the text of a test step row. @param index Row index. @param value New step text. */
  updateTestStep(index: number, value: string): void {
    this.testSteps.update(steps => steps.map((s, i) => (i === index ? value : s)));
  }

  /** Submits the new work item with only the fields relevant to its type, then closes the dialog. */
  submit(): void {
    if (!this.canSubmit()) return;
    this.board.createWorkItem({
      type:             this.type(),
      title:            this.title().trim(),
      description:      this.description(),
      priority:         this.priority(),
      assignedToId:     this.assignedToId(),
      parentId:         this.selectedParent()?.id ?? null,
      originalEstimate: this.showEstimate() ? this.originalEstimate() : 0,
      stepsToReproduce: this.isBug() ? (this.stepsToReproduce().trim() || null) : null,
      environment:      this.isBug() ? (this.environment().trim() || null)      : null,
      rootCause:        this.isBug() ? (this.rootCause().trim() || null)        : null,
      solution:         this.isBug() ? (this.solution().trim() || null)         : null,
      impaction:        this.isBug() ? (this.impaction().trim() || null)        : null,
      unitTest:         this.showDevNotes() ? (this.unitTest().trim() || null)     : null,
      designReview:     this.showDevNotes() ? (this.designReview().trim() || null) : null,
      testSteps:        this.isTestPlan() ? this.testSteps().map(s => s.trim()).filter(Boolean) : null,
      automated:        this.isTestPlan() ? this.automated() : null,
    });
    this.dialogRef.close();
  }

  /** Closes without creating. */
  cancel(): void {
    this.dialogRef.close();
  }
}
