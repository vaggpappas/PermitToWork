import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AuditEntry, PagedResult, ReferenceItem, ReferenceKind } from '../models';

@Injectable({ providedIn: 'root' })
export class ReferenceAdminApi {
  private readonly http = inject(HttpClient);

  /** Includes retired rows — the difference between this and /api/lookups. */
  list(kind: ReferenceKind, parentId?: string): Observable<ReferenceItem[]> {
    const params = parentId ? new HttpParams().set('parentId', parentId) : undefined;
    return this.http.get<ReferenceItem[]>(`/api/admin/${kind}`, { params });
  }

  createCompany(body: { code: string; name: string; kind: string }): Observable<{ id: string }> {
    return this.http.post<{ id: string }>('/api/admin/companies', body);
  }

  createFacility(body: PlaceBody): Observable<{ id: string }> {
    return this.http.post<{ id: string }>('/api/admin/facilities', body);
  }

  createBuilding(facilityId: string, body: PlaceBody): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`/api/admin/facilities/${facilityId}/buildings`, body);
  }

  createLocation(buildingId: string, body: PlaceBody): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`/api/admin/buildings/${buildingId}/locations`, body);
  }

  createLookup(
    kind: 'trades' | 'certification-types' | 'categories',
    body: { code: string; name: string },
  ): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`/api/admin/${kind}`, body);
  }

  createPermitType(body: {
    code: string;
    name: string;
    description?: string | null;
    requiredCertificationTypeIds: string[];
  }): Observable<{ id: string }> {
    return this.http.post<{ id: string }>('/api/admin/permit-types', body);
  }

  rename(
    kind: 'companies' | 'trades' | 'certification-types' | 'categories',
    id: string,
    name: string,
  ): Observable<void> {
    return this.http.put<void>(`/api/admin/${kind}/${id}`, { name });
  }

  updatePlace(
    kind: 'facilities' | 'buildings' | 'locations',
    id: string,
    body: { name: string; description?: string | null },
  ): Observable<void> {
    return this.http.put<void>(`/api/admin/${kind}/${id}`, body);
  }

  updatePermitType(
    id: string,
    body: {
      name: string;
      description?: string | null;
      requiredCertificationTypeIds: string[];
    },
  ): Observable<void> {
    return this.http.put<void>(`/api/admin/permit-types/${id}`, body);
  }

  /** Retire or restore. Nothing is ever deleted — permits and badges point at these rows. */
  setActive(kind: ReferenceKind, id: string, isActive: boolean): Observable<void> {
    return this.http.put<void>(`/api/admin/${kind}/${id}/active`, { isActive });
  }
}

export interface PlaceBody {
  code: string;
  name: string;
  description?: string | null;
}

export interface AuditSearch {
  search?: string;
  action?: string;
  entityType?: string;
  entityId?: string;
  actorEmployeeId?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

@Injectable({ providedIn: 'root' })
export class AuditApi {
  private readonly http = inject(HttpClient);

  search(query: AuditSearch): Observable<PagedResult<AuditEntry>> {
    let params = new HttpParams();

    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, String(value));
      }
    }

    return this.http.get<PagedResult<AuditEntry>>('/api/audit', { params });
  }

  forRecord(entityType: string, entityId: string): Observable<AuditEntry[]> {
    return this.http.get<AuditEntry[]>(`/api/audit/${entityType}/${entityId}`);
  }
}
