import { NgClass } from '@angular/common';
import { computed, Component, inject, input, output } from '@angular/core';
import type { BoardItem, BoardItemType } from '../../models/boards.model';
import { MembersService } from '../../services/members.service';

@Component({
  selector: 'app-task-card',
  imports: [NgClass],
  templateUrl: './task-card.component.html',
  styleUrl: './task-card.component.css',
})
export class TaskCardComponent {
  readonly task           = input.required<BoardItem>();
  readonly agingLimitDays = input<number>(0);
  readonly opened         = output<BoardItem>();
  readonly members        = inject(MembersService);

  /** Number of calendar days the item has been in its current state. Zero when stateChangedAt is null. */
  readonly agingDays = computed(() => {
    const raw = this.task().stateChangedAt;
    if (!raw) return 0;
    return Math.floor((Date.now() - new Date(raw).getTime()) / 86_400_000);
  });

  /** True when agingDays meets or exceeds the configured limit (limit > 0). */
  readonly isAging = computed(() => {
    const limit = this.agingLimitDays();
    return limit > 0 && this.agingDays() >= limit;
  });

  /** 8px type square next to the ID. */
  readonly typeSquareClass: Record<BoardItemType, string> = {
    'user-story': 'nx-type--story',
    'bug':        'nx-type--bug',
    'task':       'nx-type--task',
    'test-plan':  'nx-type--test',
  };

  readonly typeLabel: Record<BoardItemType, string> = {
    'user-story': 'User Story',
    'bug':        'Bug',
    'task':       'Task',
    'test-plan':  'Test Plan',
  };

  /** Priority dot — shown only when it is out of the ordinary (critical / high). */
  readonly priorityFlagClass: Record<BoardItem['priority'], string> = {
    critical: 'bg-rose-500',
    high:     'bg-amber-500',
    medium:   '',
    low:      '',
  };
}
