import {
  Component, input, output, computed, signal, TemplateRef, contentChild,
} from '@angular/core';
import { NgClass, NgTemplateOutlet } from '@angular/common';
import { ColumnDef, SortDirection, SortState } from '../../../models/table.model';
import { ResourcePipe } from '../../pipes/resource.pipe';

const PAGE_SIZES = [10, 25, 50];

@Component({
  selector: 'app-table',
  standalone: true,
  imports: [NgClass, NgTemplateOutlet, ResourcePipe],
  templateUrl: './app-table.component.html',
  styleUrl: './app-table.component.scss',
})
export class AppTableComponent<T extends object> {
  readonly columns  = input.required<ColumnDef<T>[]>();
  readonly data     = input<T[]>([]);
  readonly loading  = input<boolean>(false);
  readonly pageSize = input<number>(10);

  readonly rowClick = output<T>();
  readonly sortChange = output<SortState<T>>();

  /** Optional custom cell template: <ng-template #cellTpl let-row let-col="col"> */
  readonly cellTpl = contentChild<TemplateRef<{ $implicit: T; col: ColumnDef<T> }>>('cellTpl');

  protected readonly sort      = signal<SortState<T> | null>(null);
  protected readonly page      = signal(0);
  protected readonly _pageSize = signal(PAGE_SIZES[0]);
  protected readonly pageSizes = PAGE_SIZES;

  protected readonly sorted = computed(() => {
    const s = this.sort();
    const rows = [...this.data()];
    if (!s || !s.direction) return rows;
    return rows.sort((a, b) => {
      const av = a[s.key], bv = b[s.key];
      const cmp = av < bv ? -1 : av > bv ? 1 : 0;
      return s.direction === 'asc' ? cmp : -cmp;
    });
  });

  protected readonly paged = computed(() => {
    const ps = this._pageSize();
    const p  = this.page();
    return this.sorted().slice(p * ps, (p + 1) * ps);
  });

  protected readonly totalPages = computed(() =>
    Math.ceil(this.sorted().length / this._pageSize())
  );

  protected onSort(key: keyof T & string): void {
    const cur = this.sort();
    let direction: SortDirection =
      cur?.key === key ? (cur.direction === 'asc' ? 'desc' : cur.direction === 'desc' ? null : 'asc') : 'asc';
    const next = direction ? { key, direction } : null;
    this.sort.set(next);
    if (next) this.sortChange.emit(next);
  }

  protected sortIcon(key: string): string {
    const s = this.sort();
    if (!s || s.key !== key) return '↕';
    return s.direction === 'asc' ? '↑' : '↓';
  }

  protected setPage(p: number): void {
    this.page.set(Math.max(0, Math.min(p, this.totalPages() - 1)));
  }

  protected setPageSize(ps: number): void {
    this._pageSize.set(ps);
    this.page.set(0);
  }

  protected cellValue(row: T, col: ColumnDef<T>): unknown {
    return row[col.key];
  }
}
