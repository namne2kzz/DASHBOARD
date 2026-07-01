import { DecimalPipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { SprintSummaryService } from '../../services/sprint-summary.service';

@Component({
  selector: 'app-sprint-summary-panel',
  imports: [DecimalPipe],
  templateUrl: './sprint-summary-panel.component.html',
  styleUrl: './sprint-summary-panel.component.css',
})
export class SprintSummaryPanelComponent {
  readonly svc = inject(SprintSummaryService);
}
