import { prepareMermaidSourceForRender } from './mermaid-source';

describe('prepareMermaidSourceForRender', () => {
  it('escapes semicolons inside inline sequence notes', () => {
    const source = [
      'sequenceDiagram',
      '  participant A',
      '  participant B',
      '  Note over A,B: first; second; third',
    ].join('\n');

    expect(prepareMermaidSourceForRender(source)).toContain(
      'Note over A,B: first&#59; second&#59; third',
    );
  });

  it('leaves sequence notes without semicolons unchanged', () => {
    const source = [
      'sequenceDiagram',
      '  participant A',
      '  participant B',
      '  Note over A,B: first Send to P3 materializes from preload',
    ].join('\n');

    expect(prepareMermaidSourceForRender(source)).toBe(source);
  });

  it('leaves non-sequence diagrams unchanged', () => {
    const source = ['flowchart LR', '  A[one; two] --> B'].join('\n');

    expect(prepareMermaidSourceForRender(source)).toBe(source);
  });

  it('does not double-escape existing HTML entities inside sequence notes', () => {
    const source = [
      'sequenceDiagram',
      '  participant A',
      '  participant B',
      '  Note over A,B: first&#59; second; third &lt;ok&gt;',
    ].join('\n');

    expect(prepareMermaidSourceForRender(source)).toBe(
      [
        'sequenceDiagram',
        '  participant A',
        '  participant B',
        '  Note over A,B: first&#59; second&#59; third &lt;ok&gt;',
      ].join('\n'),
    );
  });

  it('preserves existing CRLF line endings', () => {
    const source = [
      'sequenceDiagram',
      '  participant A',
      '  participant B',
      '  Note over A,B: first; second',
    ].join('\r\n');

    expect(prepareMermaidSourceForRender(source)).toBe(
      [
        'sequenceDiagram',
        '  participant A',
        '  participant B',
        '  Note over A,B: first&#59; second',
      ].join('\r\n'),
    );
  });
});