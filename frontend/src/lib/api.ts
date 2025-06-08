import axios, { AxiosInstance, AxiosRequestConfig, AxiosResponse } from 'axios';
import { getAccessToken } from './auth';

// API configuration
const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';

// Create axios instance
const apiClient: AxiosInstance = axios.create({
  baseURL: API_BASE_URL,
  timeout: 30000,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Request interceptor to add auth token
apiClient.interceptors.request.use(
  async (config) => {
    try {
      const token = await getAccessToken();
      if (token) {
        config.headers.Authorization = `Bearer ${token}`;
      }
    } catch (error) {
      console.warn('Failed to get access token:', error);
    }
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

// Response interceptor for error handling
apiClient.interceptors.response.use(
  (response: AxiosResponse) => {
    return response;
  },
  (error) => {
    if (error.response?.status === 401) {
      // Handle unauthorized - redirect to login
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);

// API response types
export interface ApiResponse<T> {
  data?: T;
  success: boolean;
  message?: string;
  timestamp: string;
}

export interface PaginatedResponse<T> extends ApiResponse<T[]> {
  page: number;
  limit: number;
  total: number;
  totalPages: number;
}

// User types
export interface User {
  id: string;
  domainControllerId: string;
  adObjectGuid: string;
  samAccountName: string;
  userPrincipalName?: string;
  displayName?: string;
  givenName?: string;
  surname?: string;
  email?: string;
  department?: string;
  title?: string;
  managerDn?: string;
  status: 'active' | 'disabled' | 'locked' | 'deleted';
  lastLogon?: string;
  passwordLastSet?: string;
  accountExpires?: string;
  createdAt: string;
  updatedAt: string;
  lastSeenAt: string;
}

export interface CreateUserRequest {
  domainControllerId: string;
  adObjectGuid: string;
  samAccountName: string;
  userPrincipalName?: string;
  displayName?: string;
  givenName?: string;
  surname?: string;
  email?: string;
  department?: string;
  title?: string;
  managerDn?: string;
  status: 'active' | 'disabled' | 'locked' | 'deleted';
  lastLogon?: string;
  passwordLastSet?: string;
  accountExpires?: string;
}

export interface UpdateUserRequest {
  userPrincipalName?: string;
  displayName?: string;
  givenName?: string;
  surname?: string;
  email?: string;
  department?: string;
  title?: string;
  managerDn?: string;
  status: 'active' | 'disabled' | 'locked' | 'deleted';
  lastLogon?: string;
  passwordLastSet?: string;
  accountExpires?: string;
}

// Group types
export interface Group {
  id: string;
  domainControllerId: string;
  adObjectGuid: string;
  samAccountName: string;
  displayName?: string;
  description?: string;
  groupType: 'security' | 'distribution' | 'builtin';
  distinguishedName: string;
  createdAt: string;
  updatedAt: string;
  lastSeenAt: string;
}

export interface CreateGroupRequest {
  domainControllerId: string;
  adObjectGuid: string;
  samAccountName: string;
  displayName?: string;
  description?: string;
  groupType: 'security' | 'distribution' | 'builtin';
  distinguishedName: string;
}

export interface UpdateGroupRequest {
  displayName?: string;
  description?: string;
  groupType: 'security' | 'distribution' | 'builtin';
  distinguishedName: string;
}

// Event types
export interface Event {
  id: string;
  domainControllerId: string;
  eventType: string;
  userId?: string;
  groupId?: string;
  eventData: Record<string, any>;
  sourceIp?: string;
  userAgent?: string;
  occurredAt: string;
  createdAt: string;
}

export interface CreateEventRequest {
  domainControllerId: string;
  eventType: string;
  userId?: string;
  groupId?: string;
  eventData: Record<string, any>;
  sourceIp?: string;
  userAgent?: string;
  occurredAt: string;
}

// Query parameters
export interface PaginationParams {
  page?: number;
  limit?: number;
}

export interface UserQueryParams extends PaginationParams {
  status?: string;
  domainControllerId?: string;
}

export interface GroupQueryParams extends PaginationParams {
  groupType?: string;
  domainControllerId?: string;
}

export interface EventQueryParams extends PaginationParams {
  eventType?: string;
  userId?: string;
  groupId?: string;
  domainControllerId?: string;
  from?: string;
  to?: string;
}

// API functions
export const api = {
  // Users
  users: {
    getAll: (params?: UserQueryParams): Promise<PaginatedResponse<User>> =>
      apiClient.get('/api/v1/users', { params }).then(res => res.data),
    
    getById: (id: string): Promise<User> =>
      apiClient.get(`/api/v1/users/${id}`).then(res => res.data),
    
    create: (data: CreateUserRequest): Promise<User> =>
      apiClient.post('/api/v1/users', data).then(res => res.data),
    
    update: (id: string, data: UpdateUserRequest): Promise<User> =>
      apiClient.put(`/api/v1/users/${id}`, data).then(res => res.data),
    
    delete: (id: string): Promise<void> =>
      apiClient.delete(`/api/v1/users/${id}`).then(res => res.data),
  },

  // Groups
  groups: {
    getAll: (params?: GroupQueryParams): Promise<PaginatedResponse<Group>> =>
      apiClient.get('/api/v1/groups', { params }).then(res => res.data),
    
    getById: (id: string): Promise<Group> =>
      apiClient.get(`/api/v1/groups/${id}`).then(res => res.data),
    
    create: (data: CreateGroupRequest): Promise<Group> =>
      apiClient.post('/api/v1/groups', data).then(res => res.data),
    
    update: (id: string, data: UpdateGroupRequest): Promise<Group> =>
      apiClient.put(`/api/v1/groups/${id}`, data).then(res => res.data),
    
    delete: (id: string): Promise<void> =>
      apiClient.delete(`/api/v1/groups/${id}`).then(res => res.data),
  },

  // Events
  events: {
    getAll: (params?: EventQueryParams): Promise<PaginatedResponse<Event>> =>
      apiClient.get('/api/v1/events', { params }).then(res => res.data),
    
    create: (data: CreateEventRequest): Promise<Event> =>
      apiClient.post('/api/v1/events', data).then(res => res.data),
  },

  // Health
  health: {
    check: (): Promise<{ status: string; timestamp: string; version: string }> =>
      apiClient.get('/health').then(res => res.data),
  },
};

export default apiClient;
