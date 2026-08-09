/**
 * Chatbot request/response shapes that mirror the backend ChatDtos.
 * Kept here so the front-end never reaches into the API client service
 * directly — this gives the panel a stable contract to render against.
 */

export interface ChatHistoryMessage {
  role: 'user' | 'assistant' | 'system';
  content: string;
}

export interface ChatRequest {
  message: string;
  projectId: string | null;
  history: ChatHistoryMessage[] | null;
}

export interface ChatCitation {
  source: string;
  sourceId: string;
  title: string;
  anchor: string;
  score: number;
}

export interface ChatAnswer {
  text: string;
  citations: ChatCitation[];
}

export interface ChatStreamEvent {
  $type?: 'ChatTextEvent' | 'ChatCitationsEvent' | 'ChatDoneEvent' | 'ChatErrorEvent';
  Delta?: string;
  Citations?: ChatCitation[];
  FullText?: string;
  Message?: string;
}

export interface ReindexStatus {
  isRunning: boolean;
  lastRunAt: string | null;
  totalChunks: number;
  lastError: string | null;
}
