import { AfterViewInit, Directive, ElementRef, inject, Input, OnChanges } from '@angular/core';

/**
 * Automatically expands a `<textarea>` to fit its content — no internal scrollbar,
 * no manual resize handle. Apply as `<textarea autoResize>`.
 *
 * Responds to both user input **and** programmatic `[ngModel]` / `[value]` changes
 * via `ngOnChanges`, so the field expands correctly even when seeded from an async API
 * response rather than direct user typing.
 */
@Directive({
  selector: 'textarea[autoResize]',
  standalone: true,
  host: { '(input)': 'resize()' },
})
export class AutoResizeDirective implements AfterViewInit, OnChanges {
  private readonly el = inject(ElementRef<HTMLTextAreaElement>);

  /**
   * Receives the `[ngModel]` / `[value]` binding so programmatic model updates also
   * trigger a resize pass. Angular flows the same expression to every directive on the
   * element that declares a matching `@Input` property.
   */
  // eslint-disable-next-line @angular-eslint/no-input-rename
  @Input() ngModel: unknown;
  @Input() value: unknown;

  ngAfterViewInit(): void {
    const el = this.el.nativeElement;
    el.style.resize   = 'none';
    el.style.overflow = 'hidden';
    this.resize();
  }

  ngOnChanges(): void {
    // Defer one microtask so NgModel has already written the new value to the DOM.
    Promise.resolve().then(() => this.resize());
  }

  /** Sets height to `auto` first (so it can shrink), then to the actual scroll height. */
  resize(): void {
    const el        = this.el.nativeElement;
    el.style.height = 'auto';
    el.style.height = `${el.scrollHeight}px`;
  }
}
