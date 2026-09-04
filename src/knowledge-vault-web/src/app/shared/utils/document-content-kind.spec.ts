import {
  formatJsonContent,
  getDocumentContentKind,
  getDocumentPreviewLabel,
  getDocumentSourceLabel,
} from './document-content-kind';

describe('document content kind', () => {
  it('recognizes supported extensions case-insensitively', () => {
    expect(getDocumentContentKind('README.MD')).toBe('markdown');
    expect(getDocumentContentKind('notes.TXT')).toBe('text');
    expect(getDocumentContentKind('settings.JSON')).toBe('json');
    expect(getDocumentContentKind('workflow.HTML')).toBe('html');
  });

  it('keeps extensionless and unknown existing documents as Markdown', () => {
    expect(getDocumentContentKind('Architecture notes')).toBe('markdown');
    expect(getDocumentContentKind('data.csv')).toBe('markdown');
  });

  it('returns invalid JSON unchanged', () => {
    expect(formatJsonContent('{broken')).toBe('{broken');
  });

  it('provides consistent source labels for every content kind', () => {
    expect(getDocumentSourceLabel('markdown')).toBe('Markdown source');
    expect(getDocumentSourceLabel('text')).toBe('Plain text source');
    expect(getDocumentSourceLabel('json')).toBe('JSON source');
    expect(getDocumentSourceLabel('html')).toBe('HTML source');
  });

  it('keeps preview labels appropriate to each surface', () => {
    expect(getDocumentPreviewLabel('markdown', 'editor')).toBe('Markdown preview');
    expect(getDocumentPreviewLabel('text', 'editor')).toBe('Plain text preview');
    expect(getDocumentPreviewLabel('json', 'editor')).toBe('Formatted JSON preview');
    expect(getDocumentPreviewLabel('html', 'editor')).toBe('Static HTML preview');

    expect(getDocumentPreviewLabel('markdown', 'fullscreen')).toBe('Live preview');
    expect(getDocumentPreviewLabel('text', 'fullscreen')).toBe('Plain text');
    expect(getDocumentPreviewLabel('json', 'fullscreen')).toBe('Formatted JSON');
    expect(getDocumentPreviewLabel('html', 'fullscreen')).toBe('Static HTML');
  });
});
