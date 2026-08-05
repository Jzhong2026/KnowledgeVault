export type DocumentContentKind = 'markdown' | 'json' | 'text';
export function getDocumentContentKind(title: string | null | undefined): DocumentContentKind {
  const value = title?.trim().toLowerCase() ?? '';
  return value.endsWith('.json') ? 'json' : value.endsWith('.txt') ? 'text' : 'markdown';
}
export function formatJsonContent(value: string): string {
  try { return JSON.stringify(JSON.parse(value), null, 2); } catch { return value; }
}
