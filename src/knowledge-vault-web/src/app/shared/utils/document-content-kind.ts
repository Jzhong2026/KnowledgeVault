export type DocumentContentKind = 'markdown' | 'json' | 'text';
export type DocumentContentLabelContext = 'editor' | 'fullscreen';

export function getDocumentContentKind(title: string | null | undefined): DocumentContentKind {
  const value = title?.trim().toLowerCase() ?? '';
  return value.endsWith('.json') ? 'json' : value.endsWith('.txt') ? 'text' : 'markdown';
}

export function getDocumentSourceLabel(kind: DocumentContentKind): string {
  if (kind === 'json') return 'JSON source';
  return kind === 'text' ? 'Plain text source' : 'Markdown source';
}

export function getDocumentPreviewLabel(
  kind: DocumentContentKind,
  context: DocumentContentLabelContext,
): string {
  if (context === 'editor') {
    if (kind === 'json') return 'Formatted JSON preview';
    return kind === 'text' ? 'Plain text preview' : 'Markdown preview';
  }

  if (kind === 'json') return 'Formatted JSON';
  return kind === 'text' ? 'Plain text' : 'Live preview';
}

export function formatJsonContent(value: string): string {
  try { return JSON.stringify(JSON.parse(value), null, 2); } catch { return value; }
}
