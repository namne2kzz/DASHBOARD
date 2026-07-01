import { DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { PipelineRunStatus, PipelinesMockService } from '../../services/pipelines-mock.service';

@Component({
  selector: 'app-pipelines-page',
  imports: [DatePipe],
  templateUrl: './pipelines-page.component.html',
  styleUrl: './pipelines-page.component.css',
})
export class PipelinesPageComponent {
  readonly pipelines = inject(PipelinesMockService);

  reset(): void {
    this.pipelines.resetDemo();
  }

  statusLabel(s: PipelineRunStatus): string {
    switch (s) {
      case 'passed':
        return 'Passed';
      case 'failed':
        return 'Failed';
      case 'running':
        return 'Running';
    }
  }

  statusClass(s: PipelineRunStatus): string {
    switch (s) {
      case 'passed':
        return 'bg-emerald-500/15 text-emerald-200 ring-emerald-500/40';
      case 'failed':
        return 'bg-rose-500/15 text-rose-200 ring-rose-500/40';
      case 'running':
        return 'bg-amber-500/15 text-amber-100 ring-amber-500/40';
    }
  }
}
