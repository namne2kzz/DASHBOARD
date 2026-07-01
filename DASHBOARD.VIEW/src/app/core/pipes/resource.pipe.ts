import { Pipe, PipeTransform } from '@angular/core';
import { ResourceService, ResourceKey } from '../services/resource.service';

@Pipe({ name: 'resource', standalone: true })
export class ResourcePipe implements PipeTransform {
  constructor(private readonly rs: ResourceService) {}

  /** @param key Resource key. @param params Optional interpolation map. @returns Localized string. */
  transform(key: ResourceKey, params?: Record<string, string | number>): string {
    return this.rs.get(key, params);
  }
}
