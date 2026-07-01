import { Component, inject } from '@angular/core';
import { LoadingService } from './loading.service';
import { ResourcePipe } from '../../pipes/resource.pipe';

@Component({
  selector: 'app-loading-overlay',
  standalone: true,
  imports: [ResourcePipe],
  templateUrl: './loading-overlay.component.html',
  styleUrl: './loading-overlay.component.scss',
})
export class LoadingOverlayComponent {
  protected readonly svc = inject(LoadingService);
}
