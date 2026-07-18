import { TestBed } from '@angular/core/testing';

import { ProjectSummary } from '../../../../core/models/projects.models';
import { KnowledgeEditor } from './knowledge-editor';

describe('KnowledgeEditor', () => {
  const project: ProjectSummary = {
    id: 'project-id',
    name: 'Project Alpha',
    isArchived: false,
    currentUserRole: 'Owner',
    isFollowing: true,
    memberCount: 1,
    createdAt: '2026-07-18T00:00:00Z',
  };

  it('shows a compact project editor with a required project and optional group', async () => {
    await TestBed.configureTestingModule({
      imports: [KnowledgeEditor],
    }).compileComponents();

    const fixture = TestBed.createComponent(KnowledgeEditor);
    fixture.componentRef.setInput('workspaceScope', 'Project');
    fixture.componentRef.setInput('projects', [project]);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    const linkGroup = fixture.nativeElement.querySelector('.link-group') as HTMLFieldSetElement;

    expect(fixture.nativeElement.querySelector('h3')?.textContent?.trim()).toBe('New document');
    expect(text).not.toContain('Untitled');
    expect(text).not.toContain('Scope');
    expect(text).not.toContain('Source URL');
    expect(text).not.toContain('Change note');
    expect(text).not.toContain('New tags');
    expect(linkGroup.querySelector('legend')?.textContent).toContain('Related link');
    expect(linkGroup.textContent).toContain('Link text');
    expect(linkGroup.textContent).toContain('Link destination');
    expect(fixture.componentInstance.form.controls.projectId.hasError('required')).toBe(true);
    expect(fixture.componentInstance.form.controls.topicId.valid).toBe(true);
  });

  it('submits a project document without requiring a group', async () => {
    await TestBed.configureTestingModule({
      imports: [KnowledgeEditor],
    }).compileComponents();

    const fixture = TestBed.createComponent(KnowledgeEditor);
    fixture.componentRef.setInput('workspaceScope', 'Project');
    fixture.componentRef.setInput('projects', [project]);
    fixture.detectChanges();

    const emitted = vi.fn();
    fixture.componentInstance.saveItem.subscribe(emitted);
    fixture.componentInstance.form.patchValue({
      projectId: project.id,
      topicId: '',
      title: 'Group-free document',
      content: 'Content',
      linkDisplayText: 'Open resource',
      linkUrl: 'any destination value',
    });

    fixture.componentInstance.submit();

    expect(emitted).toHaveBeenCalledOnce();
    expect(emitted.mock.calls[0][0]).toMatchObject({
      scope: 'Project',
      projectId: project.id,
      topicId: null,
      linkDisplayText: 'Open resource',
      linkUrl: 'any destination value',
      sourceUrl: null,
      changeNote: null,
      tagNames: [],
    });
  });
});
