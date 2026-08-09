import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from '../../core/config/api.config';
import { ChatAnswer, ChatRequest, ReindexStatus } from './chat.models';

/**
 * Thin wrapper around the chatbot HTTP API. The streaming variant uses a
 * POST with `text/event-stream` because the chatbot endpoint emits a stream
 * of newline-delimited JSON events; EventSource cannot POST, so we fall
 * back to fetch + ReadableStream in the panel itself.
 */
@Injectable({ providedIn: 'root' })
export class ChatService {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  ask(req: ChatRequest): Observable<ChatAnswer> {
    return this.http.post<ChatAnswer>(`${this.base}/api/chat/messages`, req);
  }

  reindexStatus(): Observable<ReindexStatus> {
    return this.http.get<ReindexStatus>(`${this.base}/api/chat/admin/reindex/status`);
  }

  triggerReindex(): Observable<ReindexStatus> {
    return this.http.post<ReindexStatus>(`${this.base}/api/chat/admin/reindex`, {});
  }
}
