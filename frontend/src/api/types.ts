export type AuctionStatus = 'active' | 'upcoming' | 'finished' | 'cancelled';

export interface User {
  id: string;
  userName: string;
  email: string;
  role: 'User' | 'Admin';
  isActive: boolean;
  createdAt: string;
}

export interface AuthResponse {
  token: string;
  expiresAt: string;
  user: User;
}

export interface Category {
  id: number;
  name: string;
}

export interface AuctionSummary {
  id: string;
  title: string;
  category: string;
  categoryId: number;
  startingPrice: number;
  currentPrice: number;
  nextMinimumBid: number;
  bidCount: number;
  startAt: string;
  endAt: string;
  status: AuctionStatus;
  sellerName: string;
  sellerId: string;
  winnerId: string | null;
  imageUrl: string | null;
}

export interface AuctionDetail {
  id: string;
  title: string;
  description: string;
  category: string;
  categoryId: number;
  startingPrice: number;
  minIncrement: number;
  currentPrice: number;
  nextMinimumBid: number;
  bidCount: number;
  startAt: string;
  endAt: string;
  createdAt: string;
  closedAt: string | null;
  status: AuctionStatus;
  sellerId: string;
  sellerName: string;
  winnerId: string | null;
  winnerName: string | null;
  imageUrls: string[];
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface Bid {
  id: string;
  auctionId: string;
  auctionTitle: string;
  bidderId: string;
  bidderName: string;
  amount: number;
  createdAt: string;
}

export interface UserBid {
  id: string;
  auctionId: string;
  auctionTitle: string;
  amount: number;
  createdAt: string;
  auctionCurrentPrice: number;
  auctionStatus: AuctionStatus;
  isWinning: boolean;
}

export interface AppNotification {
  id: string;
  type: string;
  message: string;
  auctionId: string | null;
  isRead: boolean;
  createdAt: string;
}

export interface AdminStats {
  totalUsers: number;
  totalAuctions: number;
  activeAuctions: number;
  upcomingAuctions: number;
  finishedAuctions: number;
  cancelledAuctions: number;
  totalBids: number;
  totalAwardedAmount: number;
}

export interface CreateAuctionInput {
  title: string;
  description: string;
  categoryId: number;
  startingPrice: number;
  minIncrement: number;
  startAt: string | null;
  endAt: string;
}

export interface AuctionFilters {
  status: AuctionStatus | 'all';
  search?: string;
  categoryId?: number;
  minPrice?: number;
  maxPrice?: number;
  sort?: 'endingSoon' | 'newest' | 'priceAsc' | 'priceDesc';
  page?: number;
  pageSize?: number;
}

export interface BidPlacedEvent {
  auctionId: string;
  bid: Bid;
  currentPrice: number;
  nextMinimumBid: number;
  bidCount: number;
}

export interface AuctionClosedEvent {
  auctionId: string;
  winnerId: string | null;
  winnerName: string | null;
  finalPrice: number;
}
