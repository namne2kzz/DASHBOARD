import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { WorkItemLinkDto, WorkItemLinkType, LINK_TYPE_VALUE } from '../models/work-item-link.model';
import { WorkItemPickerApiDto } from '../models/sprint-planning-api.model';

const API = `${environment.apiBaseUrl}/repositories`;

/** CRUD for typed relationships between work items. */
@Injectable({ providedIn: 'root' })
export class WorkItemLinkService {
  private readonly http = inject(HttpClient);

  private base(repoId: string, taskId: string): string {
    return `${API}/${repoId}/sprint-tasks/${taskId}/links`;
  }

  /** Lists the links involving a work item. */
  list(repoId: string, taskId: string): Promise<WorkItemLinkDto[]> {
    return firstValueFrom(this.http.get<WorkItemLinkDto[]>(this.base(repoId, taskId)));
  }

  /** Searches work items to link to (excludes the current item). */
  searchTargets(repoId: string, taskId: string, q: string): Promise<WorkItemPickerApiDto[]> {
    const params = new HttpParams().set('q', q);
    return firstValueFrom(this.http.get<WorkItemPickerApiDto[]>(`${this.base(repoId, taskId)}/search`, { params }));
  }

  /** Creates a link from the work item to a target. */
  create(
    repoId: string, taskId: string,
    targetTaskId: string, linkType: WorkItemLinkType, description: string | null,
  ): Promise<WorkItemLinkDto> {
    // The API binds enums from their numeric value (like the rest of the project), not the name.
    return firstValueFrom(this.http.post<WorkItemLinkDto>(this.base(repoId, taskId), {
      targetTaskId, linkType: LINK_TYPE_VALUE[linkType], description,
    }));
  }

  /** Deletes a link. */
  delete(repoId: string, taskId: string, linkId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base(repoId, taskId)}/${linkId}`));
  }
}
