import { DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { ReposMockService } from '../../services/repos-mock.service';
import { ResourceService } from '../../services/resource.service';

@Component({
  selector: 'app-repos-page',
  imports: [DatePipe],
  templateUrl: './repos-page.component.html',
  styleUrl: './repos-page.component.css',
})
export class ReposPageComponent {
  readonly repos = inject(ReposMockService);
  readonly resource = inject(ResourceService);
  readonly t = this.resource.get.bind(this.resource);

  connect(): void {
    this.repos.connectGitHubApp();
  }

  disconnect(): void {
    this.repos.disconnectFromGitHub();
  }

  runDeltaSync(): void {
    this.repos.runDeltaSync();
  }

  processWebhookQueue(): void {
    this.repos.processWebhookQueue();
  }

  simulateMergeAutomation(): void {
    this.repos.simulateMergeAutomation();
  }
}
