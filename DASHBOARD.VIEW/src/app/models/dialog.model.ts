import { InjectionToken, Type } from '@angular/core';

export type DialogMode = 'normal' | 'max';

export interface DialogConfig<D = unknown> {
  title?: string;
  data?: D;
  width?: string;
  height?: string;
  mode?: DialogMode;
  closable?: boolean;
}

export interface DialogRef<R = unknown> {
  close(result?: R): void;
  readonly closed: Promise<R | undefined>;
}

export interface DialogEntry {
  id: number;
  component: Type<unknown>;
  config: Required<Pick<DialogConfig, 'mode' | 'closable'>> & DialogConfig;
  resolve: (result: unknown) => void;
}

/** Injection token for the close handle provided to dialog content components. */
export const DIALOG_REF_TOKEN = new InjectionToken<{ close(result?: unknown): void }>('DIALOG_REF');
