import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ResourceService } from '../../services/resource.service';

@Component({
  selector: 'app-pipelines-page',
  standalone: true,
  imports: [],
  templateUrl: './pipelines-page.component.html',
  styleUrl: './pipelines-page.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PipelinesPageComponent {
  readonly resource = inject(ResourceService);
  readonly t = this.resource.get.bind(this.resource);
}
