import { Injectable } from '@angular/core';
import resources from '../../resources/resources.json';

export type ResourceKey = keyof typeof resources;

@Injectable({ providedIn: 'root' })
export class ResourceService {
  private readonly values = resources as Record<string, string>;

  /**
   * Returns the localized string for the given key.
   * Supports {placeholder} interpolation via the params argument.
   * @param key Resource key from resources.json.
   * @param params Optional map of placeholder values, e.g. { count: '5' }.
   * @returns Localized string, or the key itself if not found.
   */
  get(key: ResourceKey, params?: Record<string, string | number>): string {
    let value = this.values[key] ?? key;
    if (params) {
      value = value.replace(/\{(\w+)\}/g, (_, p) => String(params[p] ?? `{${p}}`));
    }
    return value;
  }

  /**
   * Resolves multiple keys at once into a plain object.
   * @param map Object whose values are ResourceKeys and keys are your preferred aliases.
   * @returns Same shape object with resolved strings — assign to a readonly field.
   * @example
   *   readonly r = this.rs.batch({ save: 'common.save', title: 'backlog.header.title' });
   *   // template: {{ r.save }}
   */
  batch<K extends string>(map: Record<K, ResourceKey>): Record<K, string> {
    return Object.fromEntries(
      Object.entries(map).map(([alias, key]) => [alias, this.get(key as ResourceKey)])
    ) as Record<K, string>;
  }
}
