import { DocumentScope, KnowledgeItemSummary } from './knowledge.models';

export interface FolderSummary {
  id: string;
  name: string;
  description?: string | null;
  sortOrder: number;
  parentFolderId?: string | null;
  projectId?: string | null;
  scope: DocumentScope;
  childFolderCount: number;
  documentCount: number;
  isArchived: boolean;
}

export interface FolderTreeNode {
  id: string;
  name: string;
  parentFolderId?: string | null;
  sortOrder: number;
  children: FolderTreeNode[];
  /** Direct document count, populated by the workspace-page when the folder
   *  content is loaded. Optional because the raw backend tree does not carry
   *  this data. */
  documentCount?: number;
  /** Direct child folder count, populated alongside documentCount. */
  childFolderCount?: number;
  isArchived?: boolean;
}

export interface FolderContent {
  folders: FolderSummary[];
  documents: KnowledgeItemSummary[];
}

/** Paged variant of FolderContent. The workspace "Load more" UI calls this
 *  with page=1 initially, then bumps the page on each subsequent call.
 *  Backend orders by createdAt DESC so the freshest items appear first. */
export interface FolderContentPage {
  folders: FolderSummary[];
  documents: KnowledgeItemSummary[];
  page: number;
  pageSize: number;
  totalFolderCount: number;
  totalDocumentCount: number;
  hasMoreFolders: boolean;
  hasMoreDocuments: boolean;
  hasMore: boolean;
}

export interface CreateFolderRequest {
  scope: DocumentScope;
  projectId?: string | null;
  parentFolderId?: string | null;
  name: string;
  description?: string | null;
  sortOrder?: number;
}

export interface UpdateFolderRequest {
  name?: string | null;
  description?: string | null;
  parentFolderId?: string | null;
  sortOrder?: number;
}
