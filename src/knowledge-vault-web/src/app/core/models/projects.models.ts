export type ProjectRole = 'Owner' | 'Admin' | 'Editor' | 'Viewer';

export type ProjectMemorySection =
  | 'ProjectPurpose'
  | 'CurrentContext'
  | 'ConstraintsAndConventions'
  | 'KeyDecisions'
  | 'ImportantLocationsAndCommands'
  | 'AgentPrompts'
  | 'AgentHandoff'
  | 'OpenQuestions';

export type ProjectMemoryCandidateStatus = 'Pending' | 'Accepted' | 'Cancelled';

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

export interface ProjectMemoryCandidate {
  id: string;
  projectId: string;
  targetSection: ProjectMemorySection;
  proposedContent: string;
  rationale?: string | null;
  status: ProjectMemoryCandidateStatus;
  proposedByUserId: string;
  proposedByDisplayName: string;
  memoryRevisionAtProposal: number;
  reviewedByUserId?: string | null;
  reviewedByDisplayName?: string | null;
  reviewedAt?: string | null;
  appliedMemoryRevisionNumber?: number | null;
  createdAt: string;
}

export interface CreateProjectMemoryCandidateRequest {
  targetSection: ProjectMemorySection;
  proposedContent: string;
  rationale?: string | null;
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
