import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from '../config/api.config';
import {
  Category,
  Comment,
  KnowledgeItem,
  KnowledgeItemQuery,
  KnowledgeItemSummary,
  LookupItem,
  PagedResult,
  Revision,
  RevisionSummary,
  SaveCategoryRequest,
  SaveDocumentRequest,
  SaveTagRequest,
  Tag,
} from '../models/knowledge.models';
import {
  Project,
  ProjectQuery,
  ProjectSummary,
  ProjectTopic,
  SaveProjectRequest,
  SaveProjectTopicRequest,
} from '../models/projects.models';
import {
  ApiKey,
  ApiKeyCreated,
  CreateApiKeyRequest,
  UserProfile,
} from '../models/profile.models';

@Injectable({ providedIn: 'root' })
export class ApiClient {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  // ----- Categories -----
  listCategories(includeArchived = false): Observable<Category[]> {
    return this.http.get<Category[]>(`${this.baseUrl}/categories`, {
      params: new HttpParams().set('includeArchived', includeArchived),
    });
  }

  createCategory(request: SaveCategoryRequest): Observable<Category> {
    return this.http.post<Category>(`${this.baseUrl}/categories`, request);
  }

  updateCategory(id: string, request: SaveCategoryRequest): Observable<Category> {
    return this.http.put<Category>(`${this.baseUrl}/categories/${id}`, request);
  }

  deleteCategory(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/categories/${id}`);
  }

  // ----- Tags -----
  listTags(): Observable<Tag[]> {
    return this.http.get<Tag[]>(`${this.baseUrl}/tags`);
  }

  createTag(request: SaveTagRequest): Observable<Tag> {
    return this.http.post<Tag>(`${this.baseUrl}/tags`, request);
  }

  updateTag(id: string, request: SaveTagRequest): Observable<Tag> {
    return this.http.put<Tag>(`${this.baseUrl}/tags/${id}`, request);
  }

  deleteTag(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/tags/${id}`);
  }

  // ----- Documents -----
  listKnowledgeItems(query: KnowledgeItemQuery): Observable<PagedResult<KnowledgeItemSummary>> {
    let params = new HttpParams()
      .set('page', query.page ?? 1)
      .set('pageSize', query.pageSize ?? 20);

    if (query.scope) {
      params = params.set('scope', query.scope);
    }
    if (query.search) {
      params = params.set('search', query.search);
    }
    if (query.projectId) {
      params = params.set('projectId', query.projectId);
    }
    if (query.topicId) {
      params = params.set('topicId', query.topicId);
    }
    if (query.documentType) {
      params = params.set('documentType', query.documentType);
    }
    if (query.categoryId) {
      params = params.set('categoryId', query.categoryId);
    }
    if (query.status) {
      params = params.set('status', query.status);
    }
    if (query.sort) {
      params = params.set('sort', query.sort);
    }
    for (const tagId of query.tagIds ?? []) {
      params = params.append('tagIds', tagId);
    }

    return this.http.get<PagedResult<KnowledgeItemSummary>>(`${this.baseUrl}/documents`, { params });
  }

  getKnowledgeItem(id: string): Observable<KnowledgeItem> {
    return this.http.get<KnowledgeItem>(`${this.baseUrl}/documents/${id}`);
  }

  createKnowledgeItem(request: SaveDocumentRequest): Observable<KnowledgeItem> {
    return this.http.post<KnowledgeItem>(`${this.baseUrl}/documents`, request);
  }

  updateKnowledgeItem(id: string, request: SaveDocumentRequest): Observable<KnowledgeItem> {
    return this.http.put<KnowledgeItem>(`${this.baseUrl}/documents/${id}`, request);
  }

  deleteKnowledgeItem(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/documents/${id}`);
  }

  // ----- Revisions -----
  listRevisions(
    documentId: string,
    page = 1,
    pageSize = 20,
  ): Observable<PagedResult<RevisionSummary>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<RevisionSummary>>(
      `${this.baseUrl}/documents/${documentId}/revisions`,
      { params },
    );
  }

  getRevision(documentId: string, revisionNumber: number): Observable<Revision> {
    return this.http.get<Revision>(
      `${this.baseUrl}/documents/${documentId}/revisions/${revisionNumber}`,
    );
  }

  // ----- Comments -----
  listComments(
    documentId: string,
    revisionNumber: number,
    page = 1,
    pageSize = 20,
  ): Observable<PagedResult<Comment>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<Comment>>(
      `${this.baseUrl}/documents/${documentId}/revisions/${revisionNumber}/comments`,
      { params },
    );
  }

  addComment(
    documentId: string,
    revisionNumber: number,
    content: string,
  ): Observable<Comment> {
    return this.http.post<Comment>(
      `${this.baseUrl}/documents/${documentId}/revisions/${revisionNumber}/comments`,
      { content },
    );
  }

  updateComment(commentId: string, content: string): Observable<Comment> {
    return this.http.put<Comment>(`${this.baseUrl}/comments/${commentId}`, { content });
  }

  deleteComment(commentId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/comments/${commentId}`);
  }

  // ----- Projects -----
  listProjects(query: ProjectQuery = {}): Observable<PagedResult<ProjectSummary>> {
    let params = new HttpParams()
      .set('page', query.page ?? 1)
      .set('pageSize', query.pageSize ?? 20);
    if (query.search) {
      params = params.set('search', query.search);
    }
    if (query.includeArchived) {
      params = params.set('includeArchived', query.includeArchived);
    }
    return this.http.get<PagedResult<ProjectSummary>>(`${this.baseUrl}/projects`, { params });
  }

  getProject(id: string): Observable<Project> {
    return this.http.get<Project>(`${this.baseUrl}/projects/${id}`);
  }

  createProject(request: SaveProjectRequest): Observable<Project> {
    return this.http.post<Project>(`${this.baseUrl}/projects`, request);
  }

  updateProject(id: string, request: SaveProjectRequest & { isArchived?: boolean }): Observable<Project> {
    return this.http.put<Project>(`${this.baseUrl}/projects/${id}`, request);
  }

  deleteProject(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/projects/${id}`);
  }

  listTopics(
    projectId: string,
    includeArchived = false,
  ): Observable<PagedResult<ProjectTopic>> {
    const params = new HttpParams()
      .set('page', 1)
      .set('pageSize', 100)
      .set('includeArchived', includeArchived);
    return this.http.get<PagedResult<ProjectTopic>>(
      `${this.baseUrl}/projects/${projectId}/topics`,
      { params },
    );
  }

  createTopic(projectId: string, request: SaveProjectTopicRequest): Observable<ProjectTopic> {
    return this.http.post<ProjectTopic>(
      `${this.baseUrl}/projects/${projectId}/topics`,
      request,
    );
  }

  updateTopic(
    projectId: string,
    topicId: string,
    request: SaveProjectTopicRequest & { isArchived?: boolean },
  ): Observable<ProjectTopic> {
    return this.http.put<ProjectTopic>(
      `${this.baseUrl}/projects/${projectId}/topics/${topicId}`,
      request,
    );
  }

  deleteTopic(projectId: string, topicId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/projects/${projectId}/topics/${topicId}`);
  }

  // ----- Profile / API keys -----
  getProfile(): Observable<UserProfile> {
    return this.http.get<UserProfile>(`${this.baseUrl}/profile`);
  }

  updateProfile(nickname: string | null): Observable<UserProfile> {
    return this.http.put<UserProfile>(`${this.baseUrl}/profile`, { nickname });
  }

  listApiKeys(): Observable<ApiKey[]> {
    return this.http.get<ApiKey[]>(`${this.baseUrl}/profile/api-keys`);
  }

  createApiKey(request: CreateApiKeyRequest): Observable<ApiKeyCreated> {
    return this.http.post<ApiKeyCreated>(`${this.baseUrl}/profile/api-keys`, request);
  }

  revokeApiKey(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/profile/api-keys/${id}`);
  }

  // ----- Lookups -----
  listKnowledgeItemStatuses(): Observable<LookupItem[]> {
    return this.http.get<LookupItem[]>(`${this.baseUrl}/lookups/knowledge-item-statuses`);
  }
}
