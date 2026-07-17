export interface UserProfile {
  id: string;
  userName: string;
  email: string;
  nickname?: string | null;
  createdAt: string;
  lastLoginAt?: string | null;
}

export interface ApiKey {
  id: string;
  name: string;
  prefix: string;
  scopes: string[];
  createdAt: string;
  expiresAt?: string | null;
  lastUsedAt?: string | null;
  isRevoked: boolean;
}

export interface ApiKeyCreated {
  id: string;
  name: string;
  key: string;
  prefix: string;
  expiresAt: string;
}

export interface CreateApiKeyRequest {
  name: string;
  scopes: string[];
  expiresInDays?: number;
}

export const AVAILABLE_SCOPES: ReadonlyArray<{ value: string; label: string }> = [
  { value: 'documents:read', label: 'Documents (read)' },
  { value: 'documents:write', label: 'Documents (write)' },
  { value: 'comments:read', label: 'Comments (read)' },
  { value: 'comments:write', label: 'Comments (write)' },
  { value: 'projects:read', label: 'Projects (read)' },
];
