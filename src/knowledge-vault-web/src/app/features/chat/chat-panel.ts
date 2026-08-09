import { Component, ElementRef, ViewChild, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { API_BASE_URL } from '../../core/config/api.config';
import { ChatCitation, ChatStreamEvent } from './chat.models';

/**
 * Floating chat panel. Renders a fixed-position "Ask" button at the
 * bottom-right of the viewport; click expands a 420px drawer that holds
 * the message history, the streaming assistant response, and the
 * citation list.
 *
 * Streaming is handled with fetch + ReadableStream. The backend emits
 * newline-delimited JSON (one event per line). We parse each line and
 * append text fragments to the in-progress message.
 */
@Component({
  selector: 'app-chat-panel',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './chat-panel.html',
  styleUrl: './chat-panel.css',
})
export class ChatPanel {
  private readonly base = inject(API_BASE_URL);

  readonly isOpen = signal(false);
  readonly input = signal('');
  readonly isStreaming = signal(false);
  readonly messages = signal<{ role: 'user' | 'assistant'; text: string; citations?: ChatCitation[] }[]>([]);
  readonly error = signal<string | null>(null);
  readonly hasMessages = computed(() => this.messages().length > 0);

  @ViewChild('scrollHost') scrollHost?: ElementRef<HTMLDivElement>;

  toggle(): void {
    this.isOpen.update(v => !v);
  }

  close(): void {
    this.isOpen.set(false);
  }

  clear(): void {
    this.messages.set([]);
    this.error.set(null);
  }

  async send(): Promise<void> {
    const text = this.input().trim();
    if (!text || this.isStreaming()) return;
    this.error.set(null);
    const userMsg = { role: 'user' as const, text };
    this.messages.update(arr => [...arr, userMsg]);
    this.input.set('');
    const assistantIndex = this.messages().length;
    this.messages.update(arr => [...arr, { role: 'assistant', text: '' }]);
    this.isStreaming.set(true);
    this.scrollToBottom();

    try {
      const token = this.getToken();
      const resp = await fetch(`${this.base}/api/chat/messages/stream`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { 'Authorization': `Bearer ${token}` } : {})
        },
        body: JSON.stringify({ message: text, projectId: null, history: null })
      });
      if (!resp.ok || !resp.body) {
        const err = await resp.text().catch(() => resp.statusText);
        this.error.set(`Chat request failed (${resp.status}): ${err}`);
        this.isStreaming.set(false);
        return;
      }
      const reader = resp.body.getReader();
      const decoder = new TextDecoder();
      let buffer = '';
      let fullText = '';
      let citations: ChatCitation[] = [];
      while (true) {
        const { value, done } = await reader.read();
        if (done) break;
        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split('\n');
        buffer = lines.pop() ?? '';
        for (const line of lines) {
          if (!line.trim()) continue;
          let ev: ChatStreamEvent | null = null;
          try { ev = JSON.parse(line) as ChatStreamEvent; } catch { continue; }
          if (ev?.Delta) {
            fullText += ev.Delta;
            this.updateAssistant(assistantIndex, fullText);
          } else if (ev?.Citations) {
            citations = ev.Citations;
            this.updateAssistant(assistantIndex, fullText, citations);
          } else if (ev?.Message) {
            this.error.set(ev.Message);
          }
          this.scrollToBottom();
        }
      }
    } catch (e: unknown) {
      this.error.set((e as Error).message);
    } finally {
      this.isStreaming.set(false);
    }
  }

  private updateAssistant(index: number, text: string, citations?: ChatCitation[]): void {
    this.messages.update(arr => arr.map((m, i) => i === index ? { ...m, text, citations: citations ?? m.citations } : m));
  }

  private scrollToBottom(): void {
    setTimeout(() => {
      const el = this.scrollHost?.nativeElement;
      if (el) el.scrollTop = el.scrollHeight;
    }, 0);
  }

  private getToken(): string | null {
    try {
      const raw = localStorage.getItem('kv.auth.token');
      if (raw) return raw;
    } catch {}
    return null;
  }
}
