import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AccessRole,
  Address,
  EmployeeDetail,
  EmployeeSummary,
  EmploymentStatus,
  PagedResult,
  TeamSummary,
} from '../models';

export interface EmployeeSearch {
  search?: string;
  companyId?: string;
  tradeId?: string;
  status?: EmploymentStatus;
  page?: number;
  pageSize?: number;
}

export interface CreateEmployee {
  // No employeeNumber: the server generates it from the company code and a per-company
  // sequence. There is no field on the API request to supply one.
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber?: string | null;
  companyId: string;
  tradeId: string;
  jobTitle: string;
  hireDate: string;
  dateOfBirth?: string | null;
  address?: Address | null;
}

export interface UpdateEmployee {
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber?: string | null;
  jobTitle: string;
  tradeId: string;
  dateOfBirth?: string | null;
  address?: Address | null;
}

/**
 * What a person may change about themselves.
 *
 * Mirrors UpdateMyContactRequest on the server, missing fields and all. If a trade or job
 * title ever appears here it is a mistake, not a feature.
 */
export interface UpdateMyContact {
  phoneNumber?: string | null;
  address?: Address | null;
}

export interface AddCertification {
  certificationTypeId: string;
  issuedBy: string;
  issuedOn: string;
  expiresOn: string;
  referenceNumber?: string | null;
}

@Injectable({ providedIn: 'root' })
export class EmployeesApi {
  private readonly http = inject(HttpClient);

  search(query: EmployeeSearch): Observable<PagedResult<EmployeeSummary>> {
    let params = new HttpParams();

    // Only set what was actually asked for: an empty string is a filter that matches
    // nothing, which is not what an untouched search box means.
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, String(value));
      }
    }

    return this.http.get<PagedResult<EmployeeSummary>>('/api/employees', { params });
  }

  get(id: string): Observable<EmployeeDetail> {
    return this.http.get<EmployeeDetail>(`/api/employees/${id}`);
  }

  /** The signed-in user's own record. No id: the server reads it from the token. */
  me(): Observable<EmployeeDetail> {
    return this.http.get<EmployeeDetail>('/api/employees/me');
  }

  updateMyContact(body: UpdateMyContact): Observable<void> {
    return this.http.put<void>('/api/employees/me/contact', body);
  }

  create(body: CreateEmployee): Observable<{ id: string }> {
    return this.http.post<{ id: string }>('/api/employees', body);
  }

  update(id: string, body: UpdateEmployee): Observable<void> {
    return this.http.put<void>(`/api/employees/${id}`, body);
  }

  suspend(id: string): Observable<void> {
    return this.http.post<void>(`/api/employees/${id}/suspend`, {});
  }

  reinstate(id: string): Observable<void> {
    return this.http.post<void>(`/api/employees/${id}/reinstate`, {});
  }

  terminate(id: string): Observable<void> {
    return this.http.post<void>(`/api/employees/${id}/terminate`, {});
  }

  addCertification(id: string, body: AddCertification): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`/api/employees/${id}/certifications`, body);
  }

  removeCertification(id: string, certificationId: string): Observable<void> {
    return this.http.delete<void>(`/api/employees/${id}/certifications/${certificationId}`);
  }

  teams(id: string): Observable<TeamSummary[]> {
    return this.http.get<TeamSummary[]>(`/api/employees/${id}/teams`);
  }

  assignAccessRole(id: string, accessRole: AccessRole): Observable<void> {
    return this.http.put<void>(`/api/employees/${id}/access-role`, { accessRole });
  }

  /**
   * Sets or clears who this person reports to.
   *
   * `null` is a value here, not a missing argument — it is how the reporting line is
   * cleared, and the API is explicit about that. An optional parameter would make
   * "clear it" and "I forgot to pass it" the same call.
   */
  assignManager(id: string, managerId: string | null): Observable<void> {
    return this.http.put<void>(`/api/employees/${id}/manager`, { managerId });
  }
}
