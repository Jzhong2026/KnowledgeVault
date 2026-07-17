export type KnowledgeItemStatus = 'Draft' | 'Active' | 'Archived' | 'Deleted';

export type DocumentScope = 'Personal' | 'Project';

export type DocumentType = 'General' | 'PlanningReview' | 'TaskBreakdown';

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
  scope: DocumentScope;
  topicId?: string | null;
  projectId?: string | null;
  documentType: DocumentType;
  currentRevisionNumber: number;
  title: string;
  summary?: string | null;
  ticketNo?: string | null;
  status: KnowledgeItemStatus;
  category?: Category | null;
  tags: Tag[];
  createdAt: string;
  updatedAt?: string | null;
}

export interface KnowledgeItem extends KnowledgeItemSummary {
  content: string;
  sourceUrl?: string | null;
  ticketUrl?: string | null;
  changeNote?: string | null;
  publishedAt?: string | null;
  archivedAt?: string | null;
}

export interface RevisionSummary {
  id: string;
  revisionNumber: number;
  title: string;
  summary?: string | null;
  changeNote?: string | null;
  ticketNo?: string | null;
  createdByUserId: string;
  createdByUserName: string;
  createdAt: string;
}

export interface Revision extends RevisionSummary {
  content: string;
  sourceUrl?: string | null;
  ticketUrl?: string | null;
}

export interface Comment {
  id: string;
  revisionNumber: number;
  authorUserId: string;
  authorDisplayName: string;
  content: string;
  createdAt: string;
  updatedAt?: string | null;
  isDeleted: boolean;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface KnowledgeItemQuery {
  scope?: DocumentScope;
  projectId?: string;
  topicId?: string;
  documentType?: DocumentType;
  ticketNo?: string;
  search?: string;
  categoryId?: string;
  status?: KnowledgeItemStatus;
  tagIds?: string[];
  sort?: string;
  page?: number;
  pageSize?: number;
}

export interface SaveDocumentRequest {
  scope: DocumentScope;
  topicId?: string | null;
  documentType: DocumentType;
  title: string;
  content: string;
  summary?: string | null;
  sourceUrl?: string | null;
  ticketUrl?: string | null;
  changeNote?: string | null;
  categoryId?: string | null;
  status: KnowledgeItemStatus;
  tagIds?: string[];
  tagNames?: string[];
  expectedRevisionNumber?: number;
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
