import { inject, Injectable } from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import { ResourceService } from '../../services/resource.service';

@Injectable({ providedIn: 'root' })
export class ToastService {
  private readonly toastr = inject(ToastrService);
  private readonly rs     = inject(ResourceService);

  /** @param message Body text. @param title Optional title override. */
  success(message: string, title?: string): void {
    this.toastr.success(message, title ?? this.rs.get('toast.success'));
  }

  /** @param message Body text. @param title Optional title override. */
  error(message: string, title?: string): void {
    this.toastr.error(message, title ?? this.rs.get('toast.error'));
  }

  /** @param message Body text. @param title Optional title override. */
  warning(message: string, title?: string): void {
    this.toastr.warning(message, title ?? this.rs.get('toast.warning'));
  }

  /** @param message Body text. @param title Optional title override. */
  info(message: string, title?: string): void {
    this.toastr.info(message, title ?? this.rs.get('toast.info'));
  }
}
