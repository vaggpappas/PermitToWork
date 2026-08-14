// Mirrors of the API contracts. Hand-written rather than generated: the surface is small,
// and a hand-written type is a place to notice when the backend changes shape.

export type EmploymentStatus = 'Active' | 'Suspended' | 'Terminated';
export type TeamStatus = 'Active' | 'Disbanded';
export type TeamRole = 'Member' | 'Deputy' | 'Leader';

/** What a person may do. One value per employee, set by a supervisor or administrator. */
export type AccessRole = 'Employee' | 'Responsible' | 'Supervisor' | 'SafetyOfficer' | 'Administrator';

export const AccessRoles: { value: AccessRole; label: string; hint: string }[] = [
  { value: 'Employee', label: 'Employee', hint: 'Read-only' },
  { value: 'Responsible', label: 'Responsible', hint: 'Can create and change teams' },
  { value: 'Supervisor', label: 'Supervisor', hint: 'Can manage employees and assign roles' },
  { value: 'SafetyOfficer', label: 'Safety Officer', hint: 'Certifications; sees every company' },
  { value: 'Administrator', label: 'Administrator', hint: 'Everything' },
];

export interface AuthenticationResponse {
  accessToken: string;
  expiresAtUtc: string;
}

export interface Lookup {
  id: string;
  code: string;
  name: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface Address {
  street: string;
  city: string;
  postalCode: string;
  country: string;
}

export interface EmployeeSummary {
  id: string;
  employeeNumber: string;
  firstName: string;
  lastName: string;
  fullName: string;
  email: string;
  jobTitle: string;
  tradeName: string;
  companyName: string;
  status: EmploymentStatus;
  accessRole: AccessRole;
  hasAccount: boolean;
}

export interface Certification {
  id: string;
  certificationTypeId: string;
  certificationTypeName: string;
  issuedBy: string;
  issuedOn: string;
  expiresOn: string;
  referenceNumber: string | null;
  isValid: boolean;
}

export interface EmployeeDetail {
  id: string;
  employeeNumber: string;
  firstName: string;
  lastName: string;
  fullName: string;
  email: string;
  phoneNumber: string | null;
  address: Address | null;
  dateOfBirth: string | null;
  age: number | null;
  jobTitle: string;
  tradeId: string;
  tradeName: string;
  companyId: string;
  companyName: string;
  managerId: string | null;
  managerName: string | null;
  hireDate: string;
  status: EmploymentStatus;
  accessRole: AccessRole;
  hasAccount: boolean;
  certifications: Certification[];
}

export interface TeamSummary {
  id: string;
  code: string;
  name: string;
  facilityId: string;
  facilityName: string;
  status: TeamStatus;
  activeMemberCount: number;
  leaderName: string | null;
}

export interface TeamMember {
  membershipId: string;
  employeeId: string;
  employeeNumber: string;
  firstName: string;
  lastName: string;
  fullName: string;
  tradeName: string;
  companyName: string;
  role: TeamRole;
  joinedOn: string;
  leftOn: string | null;
  isCurrent: boolean;
}

export interface TeamDetail {
  id: string;
  code: string;
  name: string;
  description: string | null;
  facilityId: string;
  facilityName: string;
  status: TeamStatus;
  members: TeamMember[];
  activeMemberCount: number;
  leaderName: string | null;
}
