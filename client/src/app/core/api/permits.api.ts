import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  DocumentPolicy,
  FacilityApprover,
  Lookup,
  PagedResult,
  PermitDetail,
  PermitStatus,
  PermitSummary,
  PermitType,
} from '../models';

/** Mirrors PermitOrder on the server. */
export type PermitOrder = 'Newest' | 'Schedule';

export interface PermitSearch {
  search?: string;
  status?: PermitStatus;
  permitTypeId?: string;
  facilityId?: string;
  awaitingMyApproval?: boolean;
  raisedByMe?: boolean;
  /** Permits the signed-in user is on, as crew or as Receiver. */
  assignedToMe?: boolean;
  order?: PermitOrder;
  page?: number;
  pageSize?: number;
}

export interface CreatePermit {
  permitTypeId: string;
  categoryId: string;
  workDescription: string;
  locationId: string;
  validFrom: string;
  validTo: string;
  receiverId: string;
  project?: string | null;
  notes?: string | null;
}

@Injectable({ providedIn: 'root' })
export class PermitsApi {
  private readonly http = inject(HttpClient);

  search(query: PermitSearch): Observable<PagedResult<PermitSummary>> {
    let params = new HttpParams();

    for (const [key, value] of Object.entries(query)) {
      // false is meaningful for the boolean filters and must not be sent — "not filtering
      // by it" and "filtering for false" are different requests.
      if (value !== undefined && value !== null && value !== '' && value !== false) {
        params = params.set(key, String(value));
      }
    }

    return this.http.get<PagedResult<PermitSummary>>('/api/permits', { params });
  }

  /**
   * The permits a named employee is on.
   *
   * A different URL rather than another field on PermitSearch, mirroring the server: naming
   * somebody else is a different privilege from searching, and it carries its own role check.
   */
  assignedTo(employeeId: string, query: PermitSearch = {}): Observable<PagedResult<PermitSummary>> {
    let params = new HttpParams();

    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null && value !== '' && value !== false) {
        params = params.set(key, String(value));
      }
    }

    return this.http.get<PagedResult<PermitSummary>>(`/api/permits/assigned-to/${employeeId}`, { params });
  }

  get(id: string): Observable<PermitDetail> {
    return this.http.get<PermitDetail>(`/api/permits/${id}`);
  }

  create(body: CreatePermit): Observable<{ id: string }> {
    return this.http.post<{ id: string }>('/api/permits', body);
  }

  update(id: string, body: Omit<CreatePermit, 'permitTypeId'>): Observable<void> {
    return this.http.put<void>(`/api/permits/${id}`, body);
  }

  permitTypes(): Observable<PermitType[]> {
    return this.http.get<PermitType[]>('/api/permit-types');
  }

  categories(): Observable<Lookup[]> {
    return this.http.get<Lookup[]>('/api/categories');
  }

  /* ---------------------------------------------------------- crew and kit */

  addWorker(id: string, employeeId: string, note?: string | null): Observable<void> {
    return this.http.post<void>(`/api/permits/${id}/workers`, { employeeId, note });
  }

  removeWorker(id: string, employeeId: string): Observable<void> {
    return this.http.delete<void>(`/api/permits/${id}/workers/${employeeId}`);
  }

  addEquipment(
    id: string,
    body: { description: string; identifier?: string | null; quantity: number },
  ): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`/api/permits/${id}/equipment`, body);
  }

  removeEquipment(id: string, equipmentId: string): Observable<void> {
    return this.http.delete<void>(`/api/permits/${id}/equipment/${equipmentId}`);
  }

  /* -------------------------------------------------------------- documents */

  /** The limits, asked for rather than hard-coded, so the hint cannot contradict the server. */
  documentPolicy(): Observable<DocumentPolicy> {
    return this.http.get<DocumentPolicy>('/api/permits/document-policy');
  }

  attachDocument(id: string, file: File): Observable<{ id: string }> {
    const body = new FormData();
    body.append('file', file, file.name);

    // No Content-Type header: the browser must set it, because only the browser knows the
    // multipart boundary it is about to generate.
    return this.http.post<{ id: string }>(`/api/permits/${id}/documents`, body);
  }

  downloadDocument(id: string, documentId: string): Observable<Blob> {
    return this.http.get(`/api/permits/${id}/documents/${documentId}`, { responseType: 'blob' });
  }

  removeDocument(id: string, documentId: string): Observable<void> {
    return this.http.delete<void>(`/api/permits/${id}/documents/${documentId}`);
  }

  /* ------------------------------------------------------------- lifecycle */

  submit(id: string): Observable<void> {
    return this.http.post<void>(`/api/permits/${id}/submit`, {});
  }

  approve(id: string, comment?: string | null): Observable<void> {
    return this.http.post<void>(`/api/permits/${id}/approve`, { comment });
  }

  reject(id: string, reason: string): Observable<void> {
    return this.http.post<void>(`/api/permits/${id}/reject`, { reason });
  }

  suspend(id: string, reason: string): Observable<void> {
    return this.http.post<void>(`/api/permits/${id}/suspend`, { reason });
  }

  resume(id: string): Observable<void> {
    return this.http.post<void>(`/api/permits/${id}/resume`, {});
  }

  close(id: string, note?: string | null): Observable<void> {
    return this.http.post<void>(`/api/permits/${id}/close`, { note });
  }

  cancel(id: string, reason: string): Observable<void> {
    return this.http.post<void>(`/api/permits/${id}/cancel`, { reason });
  }
}

@Injectable({ providedIn: 'root' })
export class FacilityApproversApi {
  private readonly http = inject(HttpClient);

  panel(facilityId: string): Observable<FacilityApprover[]> {
    return this.http.get<FacilityApprover[]>(`/api/facilities/${facilityId}/approvers`);
  }

  add(facilityId: string, employeeId: string, isDecisive: boolean): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`/api/facilities/${facilityId}/approvers`, {
      employeeId,
      isDecisive,
    });
  }

  setDecisive(facilityId: string, approverId: string, isDecisive: boolean): Observable<void> {
    return this.http.put<void>(
      `/api/facilities/${facilityId}/approvers/${approverId}/decisive`,
      { isDecisive },
    );
  }

  remove(facilityId: string, approverId: string): Observable<void> {
    return this.http.delete<void>(`/api/facilities/${facilityId}/approvers/${approverId}`);
  }
}
