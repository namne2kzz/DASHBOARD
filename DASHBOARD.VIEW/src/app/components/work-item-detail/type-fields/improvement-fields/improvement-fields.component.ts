import { Component, input } from '@angular/core';
import { FormGroup } from '@angular/forms';

@Component({
  selector: 'app-improvement-fields',
  standalone: true,
  imports: [],
  templateUrl: './improvement-fields.component.html',
  styleUrl: './improvement-fields.component.scss',
})
export class ImprovementFieldsComponent {
  /** The parent work-item form group shared from WorkItemDetailComponent. */
  form = input.required<FormGroup>();
}
