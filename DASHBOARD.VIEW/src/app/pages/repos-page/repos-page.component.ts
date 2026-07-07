import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { GitRepositoryService } from '../../services/git-repository.service';
import { RepositoryContextService } from '../../services/repository-context.service';
import { ResourceService } from '../../services/resource.service';

@Component({
  selector: 'app-repos-page',
  standalone: true,
  imports: [DatePipe],
  templateUrl: './repos-page.component.html',
  styleUrl: './repos-page.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReposPageComponent {
  readonly repos = inject(GitRepositoryService);
  private readonly repoCtx = inject(RepositoryContextService);
  readonly resource = inject(ResourceService);
  readonly t = this.resource.get.bind(this.resource);

  /** Whether the connection selector should render — only meaningful when a project has 2+ configured repos. */
  readonly showSelector = computed(() => this.repos.connections().length > 1);

  /**
   * Switches the displayed overview to a different configured GitHub connection.
   * @param repoUrl Repo URL of the connection selected from the dropdown.
   */
  selectConnection(repoUrl: string): void {
    const repositoryId = this.repoCtx.selectedRepoId();
    if (repositoryId) {
      this.repos.selectConnection(repositoryId, repoUrl);
    }
  }

  /** Manually reloads the overview for the currently selected project and connection. */
  refresh(): void {
    const repositoryId = this.repoCtx.selectedRepoId();
    if (repositoryId) {
      this.repos.refresh(repositoryId);
    }
  }
}
