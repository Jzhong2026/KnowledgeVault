import { TestBed } from '@angular/core/testing';

import { KnowledgeItem } from '../../../../core/models/knowledge.models';
import { KnowledgeDetail } from './knowledge-detail';

describe('KnowledgeDetail', () => {
  it('renders the display text as a link to an unvalidated URL value', async () => {
    await TestBed.configureTestingModule({
      imports: [KnowledgeDetail],
    }).compileComponents();

    const fixture = TestBed.createComponent(KnowledgeDetail);
    const item: KnowledgeItem = {
      id: 'document-id',
      scope: 'Personal',
      ownerUserId: 'owner-id',
      ownerDisplayName: 'Owner',
      documentType: 'General',
      currentRevisionNumber: 1,
      title: 'Document',
      content: 'Content',
      linkDisplayText: 'Open related item',
      linkUrl: 'destination-without-a-scheme',
      status: 'Active',
      tags: [],
      createdAt: '2026-07-18T00:00:00Z',
    };

    fixture.componentRef.setInput('item', item);
    fixture.detectChanges();

    const link = fixture.nativeElement.querySelector('.document-link') as HTMLAnchorElement;
    expect(link.textContent?.trim()).toBe('Open related item');
    expect(link.getAttribute('href')).toBe('destination-without-a-scheme');
  });
});
