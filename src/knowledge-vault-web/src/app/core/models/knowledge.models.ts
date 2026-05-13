export type KnowledgeItemStatus = 'Draft' | 'Active' | 'Archived' | 'Deleted';

export interface Category {
  id: string;
  name: string;
  description?: string | null;
  color?: string | null;
  sortOrder: number;
  isArchived: boolean;
  createdAt: string;
  updatedAt?: string | null;
}

export interface Tag {
  id: string;
  name: string;
  color?: string | null;
  knowledgeItemCount: number;
  createdAt: string;
  updatedAt?: string | null;
}

export interface KnowledgeItemSummary {
  id: string;
  title: string;
  summary?: string | null;
  status: KnowledgeItemStatus;
  category?: Category | null;
  tags: Tag[];
  createdAt: string;
  updatedAt?: string | null;
}

export interface KnowledgeItem extends KnowledgeItemSummary {
  content: string;
  sourceUrl?: string | null;
  publishedAt?: string | null;
  archivedAt?: string | null;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface KnowledgeItemQuery {
  search?: string;
  categoryId?: string;
  status?: KnowledgeItemStatus;
  tagIds?: string[];
  page?: number;
  pageSize?: number;
}

export interface SaveKnowledgeItemRequest {
  title: string;
  content: string;
  summary?: string | null;
  sourceUrl?: string | null;
  categoryId?: string | null;
  status: KnowledgeItemStatus;
  tagIds?: string[];
  tagNames?: string[];
}

export interface SaveCategoryRequest {
  name: string;
  description?: string | null;
  color?: string | null;
  sortOrder: number;
  isArchived?: boolean;
}

export interface SaveTagRequest {
  name: string;
  color?: string | null;
}

export interface LookupItem {
  name: string;
  value: number;
}
