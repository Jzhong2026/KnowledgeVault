export type ProjectRole = 'Owner' | 'Editor' | 'Viewer';

export interface ProjectSummary {
  id: string;
  name: string;
  description?: string | null;
  isArchived: boolean;
  currentUserRole?: ProjectRole | null;
  isFollowing: boolean;
  memberCount: number;
  createdAt: string;
  updatedAt?: string | null;
}

export interface ProjectMember {
  userId: string;
  userName: string;
  email: string;
  role: ProjectRole;
  joinedAt: string;
}

export interface Project {
  id: string;
  name: string;
  description?: string | null;
  ownerUserId: string;
  isArchived: boolean;
  currentUserRole?: ProjectRole | null;
  isFollowing: boolean;
  members: ProjectMember[];
  createdAt: string;
  updatedAt?: string | null;
}

export interface ProjectTopic {
  id: string;
  projectId: string;
  name: string;
  description?: string | null;
  sortOrder: number;
  isArchived: boolean;
  createdAt: string;
  updatedAt?: string | null;
}

export interface SaveProjectRequest {
  name: string;
  description?: string | null;
}

export interface SaveProjectTopicRequest {
  name: string;
  description?: string | null;
  sortOrder?: number;
}

export type ProjectGroup = ProjectTopic;
export type SaveProjectGroupRequest = SaveProjectTopicRequest;

export interface ProjectQuery {
  search?: string;
  includeArchived?: boolean;
  followingOnly?: boolean;
  page?: number;
  pageSize?: number;
}
