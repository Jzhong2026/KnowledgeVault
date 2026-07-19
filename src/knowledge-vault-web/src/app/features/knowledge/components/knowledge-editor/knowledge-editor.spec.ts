import { TestBed } from '@angular/core/testing';

import { KnowledgeItem } from '../../../../core/models/knowledge.models';
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

  it('keeps project MEMORY.md fully read-only and does not submit direct edits', async () => {
    await TestBed.configureTestingModule({
      imports: [KnowledgeEditor],
    }).compileComponents();

    const memory: KnowledgeItem = {
      id: 'memory-id',
      scope: 'Project',
      projectId: project.id,
      topicId: null,
      ownerUserId: 'owner-id',
      ownerDisplayName: 'Owner',
      documentType: 'ProjectMemory',
      currentRevisionNumber: 1,
      title: 'MEMORY.md',
      content: '# MEMORY.md',
      summary: 'Shared durable context for project members and their agents.',
      status: 'Active',
      tags: [],
      createdAt: '2026-07-19T00:00:00Z',
    };

    const fixture = TestBed.createComponent(KnowledgeEditor);
    fixture.componentRef.setInput('workspaceScope', 'Project');
    fixture.componentRef.setInput('projects', [project]);
    fixture.componentRef.setInput('item', memory);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    expect(component.form.controls.projectId.disabled).toBe(true);
    expect(component.form.controls.topicId.disabled).toBe(true);
    expect(component.form.controls.title.disabled).toBe(true);
    expect(component.form.controls.summary.disabled).toBe(true);
    expect(component.form.controls.content.disabled).toBe(true);
    expect(component.form.controls.status.disabled).toBe(true);
    expect(fixture.nativeElement.querySelector('.danger')).toBeNull();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'System-managed shared memory',
    );

    const emitted = vi.fn();
    component.saveItem.subscribe(emitted);
    component.submit();

    expect(emitted).not.toHaveBeenCalled();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'Submit updates for review from the project workspace',
    );
  });
});
