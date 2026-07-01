import { Component, input } from '@angular/core';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';

@Component({
  selector: 'app-test-plan-fields',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './test-plan-fields.component.html',
  styleUrl: './test-plan-fields.component.scss',
})
export class TestPlanFieldsComponent {
  /** The parent work-item form group shared from WorkItemDetailComponent. */
  form = input.required<FormGroup>();

  /** Toggles the automated flag between true and false. */
  toggleAutomated(): void {
    const ctrl = this.form().get('automated');
    ctrl?.setValue(ctrl.value === true ? false : true);
  }
}
