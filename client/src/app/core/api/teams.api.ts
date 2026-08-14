import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Lookup, PagedResult, TeamDetail, TeamRole, TeamStatus, TeamSummary } from '../models';

export interface TeamSearch {
  search?: string;
  facilityId?: string;
  status?: TeamStatus;
  page?: number;
  pageSize?: number;
}

export interface CreateTeam {
  // No code: generated server-side as <three letters>-<year>-<sequence>.
  name: string;
  description?: string | null;
  facilityId: string;
  leaderEmployeeId: string;
}

@Injectable({ providedIn: 'root' })
export class TeamsApi {
  private readonly http = inject(HttpClient);

  search(query: TeamSearch): Observable<PagedResult<TeamSummary>> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, String(value));
      }
    }

    return this.http.get<PagedResult<TeamSummary>>('/api/teams', { params });
  }

  get(id: string): Observable<TeamDetail> {
    return this.http.get<TeamDetail>(`/api/teams/${id}`);
  }

  create(body: CreateTeam): Observable<{ id: string }> {
    return this.http.post<{ id: string }>('/api/teams', body);
  }

  update(id: string, body: { name: string; description?: string | null }): Observable<void> {
    return this.http.put<void>(`/api/teams/${id}`, body);
  }

  addMember(id: string, employeeId: string, role: TeamRole): Observable<void> {
    return this.http.post<void>(`/api/teams/${id}/members`, { employeeId, role });
  }

  removeMember(id: string, employeeId: string): Observable<void> {
    // The API takes an optional leaving date; omitted means today.
    return this.http.delete<void>(`/api/teams/${id}/members/${employeeId}`, { body: {} });
  }

  changeMemberRole(id: string, employeeId: string, role: TeamRole): Observable<void> {
    return this.http.put<void>(`/api/teams/${id}/members/${employeeId}/role`, { role });
  }

  disband(id: string): Observable<void> {
    return this.http.post<void>(`/api/teams/${id}/disband`, {});
  }
}

@Injectable({ providedIn: 'root' })
export class LookupsApi {
  private readonly http = inject(HttpClient);

  companies(): Observable<Lookup[]> {
    return this.http.get<Lookup[]>('/api/lookups/companies');
  }

  trades(): Observable<Lookup[]> {
    return this.http.get<Lookup[]>('/api/lookups/trades');
  }

  certificationTypes(): Observable<Lookup[]> {
    return this.http.get<Lookup[]>('/api/lookups/certification-types');
  }

  facilities(): Observable<Lookup[]> {
    return this.http.get<Lookup[]>('/api/lookups/facilities');
  }
}
