import {
  AfterViewInit, Component, ElementRef, HostListener, input, signal, TemplateRef,
  contentChild, viewChild,
} from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { FlipDropDirective } from '../../../directives/flip-drop.directive';

@Component({
  selector: 'app-popup',
  standalone: true,
  imports: [NgTemplateOutlet, FlipDropDirective],
  templateUrl: './app-popup.component.html',
  styleUrl: './app-popup.component.scss',
  host: { class: 'relative inline-block' },
})
export class AppPopupComponent {
  /** Position of the popup panel relative to the trigger. */
  readonly position = input<'bottom-left' | 'bottom-right' | 'top-left' | 'top-right'>('bottom-left');

  /** The trigger element — slot via <ng-template #trigger> inside <app-popup>. */
  readonly triggerTpl = contentChild<TemplateRef<unknown>>('trigger');

  /** The panel content — slot via <ng-template #panel> inside <app-popup>. */
  readonly panelTpl = contentChild<TemplateRef<unknown>>('panel');

  protected readonly open = signal(false);

  protected toggle(): void { this.open.update(v => !v); }

  protected positionClass(): string {
    const map: Record<string, string> = {
      'bottom-left':  'top-full left-0 mt-1',
      'bottom-right': 'top-full right-0 mt-1',
      'top-left':     'bottom-full left-0 mb-1',
      'top-right':    'bottom-full right-0 mb-1',
    };
    return map[this.position()];
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.open() && !this._el.nativeElement.contains(event.target as Node)) {
      this.open.set(false);
    }
  }

  constructor(private readonly _el: ElementRef<HTMLElement>) {}
}
