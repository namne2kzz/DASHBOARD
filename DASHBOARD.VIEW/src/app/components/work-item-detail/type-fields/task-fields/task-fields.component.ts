import { Component, input } from '@angular/core';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';

@Component({
  selector: 'app-task-fields',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './task-fields.component.html',
  styleUrl: './task-fields.component.scss',
})
export class TaskFieldsComponent {
  /** The parent work-item form group shared from WorkItemDetailComponent. */
  form = input.required<FormGroup>();
}
