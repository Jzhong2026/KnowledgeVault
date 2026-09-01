const SEQUENCE_DIAGRAM_PATTERN = /(^|\n)\s*sequenceDiagram\b/;
const INLINE_SEQUENCE_NOTE_PATTERN =
  /^(\s*Note\s+(?:over|left of|right of)\s+[^:]+:\s*)(.*;.*)$/;
const HTML_ENTITY_OR_SEMICOLON_PATTERN = /&(?:#\d+|#x[\da-f]+|[a-z][a-z0-9]+);|;/gi;

// Mermaid 11 rejects raw semicolons inside inline sequence notes. Escaping
// just the note body keeps the stored Markdown untouched while restoring
// compatibility with documents authored against older Mermaid behavior.
export function prepareMermaidSourceForRender(source: string): string {
  if (!source.includes(';') || !source.includes('Note') || !SEQUENCE_DIAGRAM_PATTERN.test(source)) {
    return source;
  }

  const lineBreak = source.includes('\r\n') ? '\r\n' : '\n';
  let changed = false;

  const normalized = source.split(/\r?\n/).map((line) => {
    const match = line.match(INLINE_SEQUENCE_NOTE_PATTERN);

    if (!match) {
      return line;
    }

    changed = true;

    const [, prefix, text] = match;

    return `${prefix}${escapeInlineSequenceNoteSemicolons(text)}`;
  });

  return changed ? normalized.join(lineBreak) : source;
}

function escapeInlineSequenceNoteSemicolons(text: string): string {
  return text.replace(HTML_ENTITY_OR_SEMICOLON_PATTERN, (segment) =>
    segment === ';' ? '&#59;' : segment,
  );
}