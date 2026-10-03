import type {
  AdminStats,
  AppNotification,
  AuctionDetail,
  AuctionFilters,
  AuctionSummary,
  AuthResponse,
  Bid,
  Category,
  CreateAuctionInput,
  PagedResult,
  User,
  UserBid,
} from './types';

function trimTrailingSlashes(url: string): string {
  let end = url.length;
  while (end > 0 && url[end - 1] === '/') end--;
  return url.slice(0, end);
}

export const API_URL: string = trimTrailingSlashes(import.meta.env.VITE_API_URL ?? 'http://localhost:5270');

const STORAGE_KEY = 'subasta.session';

export const tokenStore = {
  get: (): string | null => localStorage.getItem(STORAGE_KEY),
  set: (token: string) => localStorage.setItem(STORAGE_KEY, token),
  clear: () => localStorage.removeItem(STORAGE_KEY),
};

/** Error devuelto por la API con el formato ProblemDetails. */
export class ApiError extends Error {
  readonly status: number;
  readonly fieldErrors: Record<string, string[]>;

  constructor(status: number, message: string, fieldErrors: Record<string, string[]> = {}) {
    super(message);
    this.status = status;
    this.fieldErrors = fieldErrors;
  }
}

export function resolveUrl(path: string | null | undefined): string | undefined {
  if (!path) return undefined;
  return path.startsWith('/') ? `${API_URL}${path}` : path;
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  const token = tokenStore.get();
  if (token) headers.set('Authorization', `Bearer ${token}`);
  if (init.body && !(init.body instanceof FormData)) headers.set('Content-Type', 'application/json');

  const response = await fetch(`${API_URL}${path}`, { ...init, headers });

  if (response.status === 204) return undefined as T;

  const isJson = response.headers.get('content-type')?.includes('json');
  const body: unknown = isJson ? await response.json() : null;

  if (!response.ok) {
    const problem = (body ?? {}) as { title?: string; detail?: string; errors?: Record<string, string[]> };
    const fieldErrors = problem.errors ?? {};
    const firstFieldError = Object.values(fieldErrors)[0]?.[0];
    const message =
      problem.detail ??
      firstFieldError ??
      problem.title ??
      (response.status === 401 ? 'Debe iniciar sesión.' : `Error ${response.status}`);
    if (response.status === 401 && token) tokenStore.clear();
    throw new ApiError(response.status, message, fieldErrors);
  }

  return body as T;
}

function toQuery(params: Record<string, string | number | boolean | undefined | null>): string {
  const search = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') search.set(key, String(value));
  });
  const text = search.toString();
  return text ? `?${text}` : '';
}

const json = (data: unknown) => JSON.stringify(data);

export const api = {
  register: (userName: string, email: string, password: string) =>
    request<AuthResponse>('/auth/register', { method: 'POST', body: json({ userName, email, password }) }),
  login: (email: string, password: string) =>
    request<AuthResponse>('/auth/login', { method: 'POST', body: json({ email, password }) }),
  me: () => request<User>('/auth/me'),

  categories: () => request<Category[]>('/categories'),
  auctions: (filters: AuctionFilters) =>
    request<PagedResult<AuctionSummary>>(`/auctions${toQuery({ ...filters })}`),
  auction: (id: string) => request<AuctionDetail>(`/auctions/${encodeURIComponent(id)}`),
  createAuction: (input: CreateAuctionInput) =>
    request<AuctionDetail>('/auctions', { method: 'POST', body: json(input) }),
  uploadImage: (auctionId: string, file: File) => {
    const form = new FormData();
    form.append('file', file);
    return request<{ url: string }>(`/auctions/${encodeURIComponent(auctionId)}/images`, { method: 'POST', body: form });
  },
  cancelAuction: (id: string) => request<void>(`/auctions/${encodeURIComponent(id)}/cancel`, { method: 'POST' }),

  placeBid: (auctionId: string, amount: number) =>
    request<Bid>('/bids', { method: 'POST', body: json({ auctionId, amount }) }),
  bids: (auctionId: string) => request<Bid[]>(`/bids${toQuery({ auctionId })}`),

  userAuctions: (type: 'published' | 'participating' | 'won') =>
    request<AuctionSummary[]>(`/user/auctions${toQuery({ type })}`),
  userBids: () => request<UserBid[]>('/user/bids'),
  notifications: () => request<AppNotification[]>('/user/notifications'),
  markNotificationRead: (id: string) =>
    request<void>(`/user/notifications/${encodeURIComponent(id)}/read`, { method: 'POST' }),
  markAllNotificationsRead: () => request<void>('/user/notifications/read-all', { method: 'POST' }),

  adminStats: () => request<AdminStats>('/admin/stats'),
  adminUsers: (search?: string) => request<User[]>(`/admin/users${toQuery({ search })}`),
  adminUpdateUser: (id: string, changes: { role?: string; isActive?: boolean }) =>
    request<User>(`/admin/users/${encodeURIComponent(id)}`, { method: 'PATCH', body: json(changes) }),
  adminAuctions: (filters: AuctionFilters) =>
    request<PagedResult<AuctionSummary>>(`/admin/auctions${toQuery({ ...filters })}`),
  adminCloseAuction: (id: string) =>
    request<void>(`/admin/auctions/${encodeURIComponent(id)}/close`, { method: 'POST' }),
  adminCancelAuction: (id: string) =>
    request<void>(`/admin/auctions/${encodeURIComponent(id)}/cancel`, { method: 'POST' }),
};
