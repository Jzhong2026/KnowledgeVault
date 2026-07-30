import { describe, expect, it } from 'vitest';

import { buildRevisionDiff } from './revision-diff';

describe('buildRevisionDiff', () => {
  it('pairs changed lines and highlights their changed words', () => {
    const blocks = buildRevisionDiff('hello old world', 'hello new world');
    const row = blocks[0].rows[0];

    expect(row.kind).toBe('changed');
    expect(row.oldFragments).toContainEqual({ text: 'old', kind: 'removed' });
    expect(row.newFragments).toContainEqual({ text: 'new', kind: 'added' });
  });

  it('collapses unchanged lines while retaining three lines of context', () => {
    const lines = Array.from({ length: 10 }, (_, index) => `line ${index + 1}`).join('\n');
    const blocks = buildRevisionDiff(lines, `${lines}\nnew line`);

    expect(blocks.filter((block) => block.kind === 'collapsed')[0].rows).toHaveLength(4);
    expect(blocks.filter((block) => block.kind === 'rows').flatMap((block) => block.rows)).toHaveLength(7);
  });
});
