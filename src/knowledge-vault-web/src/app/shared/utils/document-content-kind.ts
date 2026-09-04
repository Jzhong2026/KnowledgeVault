export type DocumentContentKind = 'markdown' | 'json' | 'text' | 'html';
export type DocumentContentLabelContext = 'editor' | 'fullscreen';

export function getDocumentContentKind(title: string | null | undefined): DocumentContentKind {
  const value = title?.trim().toLowerCase() ?? '';
  if (value.endsWith('.html')) return 'html';
  return value.endsWith('.json') ? 'json' : value.endsWith('.txt') ? 'text' : 'markdown';
}

export function getDocumentSourceLabel(kind: DocumentContentKind): string {
  if (kind === 'json') return 'JSON source';
  if (kind === 'text') return 'Plain text source';
  return kind === 'html' ? 'HTML source' : 'Markdown source';
}

export function getDocumentPreviewLabel(
  kind: DocumentContentKind,
  context: DocumentContentLabelContext,
): string {
  if (context === 'editor') {
    if (kind === 'json') return 'Formatted JSON preview';
    if (kind === 'text') return 'Plain text preview';
    return kind === 'html' ? 'Static HTML preview' : 'Markdown preview';
  }

  if (kind === 'json') return 'Formatted JSON';
  if (kind === 'text') return 'Plain text';
  return kind === 'html' ? 'Static HTML' : 'Live preview';
}

export function formatJsonContent(value: string): string {
  try { return JSON.stringify(JSON.parse(value), null, 2); } catch { return value; }
}
