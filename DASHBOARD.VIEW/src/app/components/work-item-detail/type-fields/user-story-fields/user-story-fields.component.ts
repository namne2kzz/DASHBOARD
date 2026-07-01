import { Component, input } from '@angular/core';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';

@Component({
  selector: 'app-user-story-fields',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './user-story-fields.component.html',
  styleUrl: './user-story-fields.component.scss',
})
export class UserStoryFieldsComponent {
  /** The parent work-item form group shared from WorkItemDetailComponent. */
  form = input.required<FormGroup>();
}