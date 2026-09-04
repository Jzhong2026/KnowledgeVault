import { findExistingImportDocument } from './import-document-match';

function docs(...titles: string[]): Array<{ id: string; title: string }> {
  return titles.map((title, index) => ({ id: `doc-${index}`, title }));
}

describe('findExistingImportDocument', () => {
  it('matches the full dropped filename first', () => {
    const documents = docs('test', 'test.txt');

    expect(findExistingImportDocument(documents, 'test.txt')?.title).toBe('test.txt');
  });

  it('matches a document titled with the filename stem when the exact name is absent', () => {
    const documents = docs('test');

    expect(findExistingImportDocument(documents, 'test.txt')?.title).toBe('test');
  });

  it('does not auto-select when several documents share the same filename stem and none match exactly', () => {
    const documents = docs('test', 'test.md');

    expect(findExistingImportDocument(documents, 'test.txt')).toBeUndefined();
  });

  it('does not treat a different extension as a stem match', () => {
    expect(findExistingImportDocument(docs('test.md'), 'test.txt')).toBeUndefined();
  });

  it('returns undefined when the folder has no related document', () => {
    expect(findExistingImportDocument(docs('notes.md'), 'test.txt')).toBeUndefined();
  });
});
