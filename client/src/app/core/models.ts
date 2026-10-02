// mirrors the API DTOs. enums are strings on the wire

export type Borough = 'Manhattan' | 'Brooklyn' | 'Queens' | 'Bronx' | 'StatenIsland';
export type Priority = 'Low' | 'Medium' | 'High' | 'Critical';
export type OutageStatus = 'Reported' | 'Investigating' | 'Restoring' | 'Resolved';
export type WorkOrderStatus = 'Open' | 'Assigned' | 'InProgress' | 'Completed' | 'Cancelled';
export type UserRole = 'Dispatcher' | 'Crew';
export type SortDir = 'asc' | 'desc';
export type OutageSortField = 'reportedAt' | 'priority' | 'customersAffected';

export const BOROUGHS: Borough[] = ['Manhattan', 'Brooklyn', 'Queens', 'Bronx', 'StatenIsland'];
export const PRIORITIES: Priority[] = ['Critical', 'High', 'Medium', 'Low'];
export const OUTAGE_STATUSES: OutageStatus[] = ['Reported', 'Investigating', 'Restoring', 'Resolved'];
export const ACTIVE_OUTAGE_STATUSES: OutageStatus[] = ['Reported', 'Investigating', 'Restoring'];

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface OutageSummary {
  id: number;
  title: string;
  borough: Borough;
  neighborhood: string;
  status: OutageStatus;
  priority: Priority;
  customersAffected: number;
  reportedAt: string;
  resolvedAt: string | null;
  openWorkOrders: number;
}

export interface OutageDetail extends Omit<OutageSummary, 'openWorkOrders'> {
  description: string | null;
  createdAt: string;
  updatedAt: string;
  workOrders: WorkOrder[];
}

export interface WorkOrder {
  id: number;
  outageId: number;
  outageTitle: string;
  borough: Borough;
  neighborhood: string;
  title: string;
  notes: string | null;
  status: WorkOrderStatus;
  priority: Priority;
  crewId: number | null;
  crewName: string | null;
  completedAt: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface Crew {
  id: number;
  name: string;
  homeBorough: Borough;
  isActive: boolean;
  members: number;
  openWorkOrders: number;
}

export interface CurrentUser {
  id: number;
  email: string;
  displayName: string;
  role: UserRole;
  crewId: number | null;
  crewName: string | null;
}

export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  user: CurrentUser;
}

export interface OutageQuery {
  status?: OutageStatus[];
  priority?: Priority[];
  borough?: Borough[];
  search?: string;
  from?: string;
  to?: string;
  sortBy?: OutageSortField;
  sortDir?: SortDir;
  page?: number;
  pageSize?: number;
}

export interface WorkOrderQuery {
  status?: WorkOrderStatus[];
  crewId?: number;
  outageId?: number;
  sortDir?: SortDir;
  page?: number;
  pageSize?: number;
}

export interface CreateOutageRequest {
  title: string;
  description?: string | null;
  borough: Borough;
  neighborhood: string;
  customersAffected: number;
  priority: Priority;
}

export interface CreateWorkOrderRequest {
  title: string;
  notes?: string | null;
  priority?: Priority | null;
  crewId?: number | null;
}

// RFC 9457 body the API returns for every error
export interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
  traceId?: string;
}

export const boroughLabel = (b: Borough) => (b === 'StatenIsland' ? 'Staten Island' : b);
export const statusLabel = (s: string) => (s === 'InProgress' ? 'In progress' : s);
