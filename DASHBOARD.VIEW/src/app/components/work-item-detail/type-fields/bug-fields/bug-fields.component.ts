import { Component, input } from '@angular/core';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';

@Component({
  selector: 'app-bug-fields',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './bug-fields.component.html',
  styleUrl: './bug-fields.component.scss',
})
export class BugFieldsComponent {
  /** The parent work-item form group shared from WorkItemDetailComponent. */
  form = input.required<FormGroup>();
}
