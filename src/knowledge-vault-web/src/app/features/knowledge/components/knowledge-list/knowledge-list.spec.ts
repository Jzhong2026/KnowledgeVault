import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { KnowledgeItemSummary } from '../../../../core/models/knowledge.models';
import { KnowledgeList } from './knowledge-list';

describe('KnowledgeList', () => {
  it('routes project and personal documents through their matching navigation sections', async () => {
    const createItem = (
      id: string,
      scope: KnowledgeItemSummary['scope'],
    ): KnowledgeItemSummary => ({
      id,
      scope,
      ownerUserId: 'owner-id',
      ownerDisplayName: 'Owner',
      documentType: 'General',
      currentRevisionNumber: 1,
      title: `${scope} document`,
      status: 'Active',
      tags: [],
      createdAt: '2026-07-18T00:00:00Z',
    });

    await TestBed.configureTestingModule({
      imports: [KnowledgeList],
      providers: [provideRouter([])],
    }).compileComponents();

    const fixture = TestBed.createComponent(KnowledgeList);
    fixture.componentRef.setInput('items', [
      createItem('project-id', 'Project'),
      createItem('personal-id', 'Personal'),
    ]);
    fixture.detectChanges();

    const links = Array.from(
      fixture.nativeElement.querySelectorAll('.title-link'),
    ) as HTMLAnchorElement[];

    expect(links.map((link) => link.getAttribute('href'))).toEqual([
      '/project-documents/detail/project-id',
      '/knowledge/detail/personal-id',
    ]);
  });
});
