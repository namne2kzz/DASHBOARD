import { NgClass } from '@angular/common';
import { computed, Component, inject, input, output } from '@angular/core';
import { DragDropModule } from '@angular/cdk/drag-drop';
import type { BoardItem, BoardItemType } from '../../models/boards.model';
import { SPRINT_TASK_STATE_BADGE, SPRINT_TASK_STATE_LABEL } from '../../core/constants/system.constant';
import { MembersService } from '../../services/members.service';

@Component({
  selector: 'app-task-card',
  imports: [DragDropModule, NgClass],
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

  readonly stateBadge = SPRINT_TASK_STATE_BADGE;
  readonly stateLabel = SPRINT_TASK_STATE_LABEL;

  readonly typeIconClass: Record<BoardItemType, string> = {
    'user-story': 'text-sky-400 ring-sky-500/35 bg-sky-500/10',
    'bug':        'text-rose-400 ring-rose-500/35 bg-rose-500/10',
    'task':       'text-amber-300 ring-amber-400/35 bg-amber-400/10',
    'test-plan':  'text-violet-400 ring-violet-500/35 bg-violet-500/10',
  };

  readonly leftBorderClass: Record<BoardItemType, string> = {
    'user-story': 'border-l-sky-500',
    'bug':        'border-l-rose-500',
    'task':       'border-l-amber-400',
    'test-plan':  'border-l-violet-500',
  };

  readonly priorityDotClass: Record<BoardItem['priority'], string> = {
    critical: 'bg-red-500',
    high:     'bg-orange-400',
    medium:   'bg-amber-400',
    low:      'bg-slate-500',
  };
}
