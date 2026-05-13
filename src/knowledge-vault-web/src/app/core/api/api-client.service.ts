import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from '../config/api.config';
import {
  Category,
  KnowledgeItem,
  KnowledgeItemQuery,
  KnowledgeItemSummary,
  LookupItem,
  PagedResult,
  SaveCategoryRequest,
  SaveKnowledgeItemRequest,
  SaveTagRequest,
  Tag,
} from '../models/knowledge.models';

@Injectable({ providedIn: 'root' })
export class ApiClient {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

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

  listKnowledgeItems(query: KnowledgeItemQuery): Observable<PagedResult<KnowledgeItemSummary>> {
    let params = new HttpParams()
      .set('page', query.page ?? 1)
      .set('pageSize', query.pageSize ?? 20);

    if (query.search) {
      params = params.set('search', query.search);
    }

    if (query.categoryId) {
      params = params.set('categoryId', query.categoryId);
    }

    if (query.status) {
      params = params.set('status', query.status);
    }

    for (const tagId of query.tagIds ?? []) {
      params = params.append('tagIds', tagId);
    }

    return this.http.get<PagedResult<KnowledgeItemSummary>>(`${this.baseUrl}/knowledge-items`, { params });
  }

  getKnowledgeItem(id: string): Observable<KnowledgeItem> {
    return this.http.get<KnowledgeItem>(`${this.baseUrl}/knowledge-items/${id}`);
  }

  createKnowledgeItem(request: SaveKnowledgeItemRequest): Observable<KnowledgeItem> {
    return this.http.post<KnowledgeItem>(`${this.baseUrl}/knowledge-items`, request);
  }

  updateKnowledgeItem(id: string, request: SaveKnowledgeItemRequest): Observable<KnowledgeItem> {
    return this.http.put<KnowledgeItem>(`${this.baseUrl}/knowledge-items/${id}`, request);
  }

  deleteKnowledgeItem(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/knowledge-items/${id}`);
  }

  listKnowledgeItemStatuses(): Observable<LookupItem[]> {
    return this.http.get<LookupItem[]>(`${this.baseUrl}/lookups/knowledge-item-statuses`);
  }
}
