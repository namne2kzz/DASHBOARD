import { Injectable, signal } from '@angular/core';
import { StorageKeys } from '../core/constants/storage-keys.constant';

export type PipelineRunStatus = 'passed' | 'failed' | 'running';

export interface PipelineRun {
  id: string;
  pipelineName: string;
  buildNumber: string;
  status: PipelineRunStatus;
  /** 0–100; meaningful when running */
  progress: number;
  startedAt: string;
  branch: string;
}

const SEED: PipelineRun[] = [
  {
    id: 'b1',
    pipelineName: 'task-dashboard-ci',
    buildNumber: '20260514.3',
    status: 'passed',
    progress: 100,
    startedAt: '2026-05-14T08:00:00Z',
    branch: 'main',
  },
  {
    id: 'b2',
    pipelineName: 'task-dashboard-ci',
    buildNumber: '20260514.2',
    status: 'failed',
    progress: 100,
    startedAt: '2026-05-14T07:40:00Z',
    branch: 'feature/ado-shell',
  },
  {
    id: 'b3',
    pipelineName: 'task-dashboard-ci',
    buildNumber: '20260514.1',
    status: 'running',
    progress: 62,
    startedAt: '2026-05-14T07:55:00Z',
    branch: 'main',
  },
];

@Injectable({ providedIn: 'root' })
export class PipelinesMockService {
  readonly runs = signal<PipelineRun[]>([]);

  constructor() {
    this.load();
  }

  private load(): void {
    const raw = localStorage.getItem(StorageKeys.pipelines);
    if (!raw) {
      localStorage.setItem(StorageKeys.pipelines, JSON.stringify(SEED));
      this.runs.set(SEED);
      return;
    }
    try {
      const parsed = JSON.parse(raw) as PipelineRun[];
      this.runs.set(Array.isArray(parsed) && parsed.length ? parsed : SEED);
    } catch {
      localStorage.setItem(StorageKeys.pipelines, JSON.stringify(SEED));
      this.runs.set(SEED);
    }
  }

  resetDemo(): void {
    localStorage.setItem(StorageKeys.pipelines, JSON.stringify(SEED));
    this.runs.set(SEED);
  }
}
