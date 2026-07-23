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
}

export interface FolderTreeNode {
  id: string;
  name: string;
  parentFolderId?: string | null;
  sortOrder: number;
  children: FolderTreeNode[];
}

export interface FolderContent {
  folders: FolderSummary[];
  documents: KnowledgeItemSummary[];
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
