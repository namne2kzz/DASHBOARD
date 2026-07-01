import { Injectable } from '@angular/core';
import resources from '../resources/resources.json';

@Injectable({ providedIn: 'root' })
export class ResourceService {
  private readonly values = resources as Record<string, string>;

  get(key: string, fallback = key): string {
    return this.values[key] ?? fallback;
  }
}
