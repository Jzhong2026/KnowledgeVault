import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import {
  Category,
  DocumentScope,
  DocumentType,
  KnowledgeItem,
  KnowledgeItemStatus,
  SaveDocumentRequest,
  Tag,
} from '../../../../core/models/knowledge.models';
import { ProjectSummary, ProjectTopic } from '../../../../core/models/projects.models';

@Component({
  selector: 'app-knowledge-editor',
  imports: [ReactiveFormsModule],
  templateUrl: './knowledge-editor.html',
  styleUrl: './knowledge-editor.css',
})
export class KnowledgeEditor implements OnChanges {
  @Input() item: KnowledgeItem | null = null;
  @Input() categories: Category[] = [];
  @Input() tags: Tag[] = [];
  @Input() projects: ProjectSummary[] = [];
  @Input() topics: ProjectTopic[] = [];
  @Input() saving = false;
  @Input() workspaceScope: DocumentScope = 'Personal';
  @Input() defaultProjectId: string | null = null;
  @Input() defaultTopicId: string | null = null;

  @Output() saveItem = new EventEmitter<SaveDocumentRequest>();
  @Output() deleteItem = new EventEmitter<void>();
  @Output() closeDialog = new EventEmitter<void>();
  @Output() projectSelected = new EventEmitter<string>();

  readonly statuses: KnowledgeItemStatus[] = ['Draft', 'Active', 'Archived'];

  readonly form = new FormBuilder().nonNullable.group({
    scope: ['Personal' as DocumentScope, [Validators.required]],
    projectId: [''],
    topicId: [''],
    documentType: ['General' as DocumentType, [Validators.required]],
    title: ['', [Validators.required, Validators.maxLength(256)]],
    summary: [''],
    content: ['', [Validators.required]],
    sourceUrl: [''],
    ticketUrl: [''],
    changeNote: [''],
    categoryId: [''],
    status: ['Draft' as KnowledgeItemStatus, [Validators.required]],
    tagIds: [[] as string[]],
    tagNames: [''],
  });

  ngOnChanges(changes: SimpleChanges): void {
    if (
      !changes['item'] &&
      !changes['workspaceScope'] &&
      !changes['defaultProjectId'] &&
      !changes['defaultTopicId']
    ) {
      return;
    }

    if (!this.item) {
      this.form.reset({
        scope: this.workspaceScope,
        projectId: this.workspaceScope === 'Project' ? this.defaultProjectId ?? '' : '',
        topicId: this.workspaceScope === 'Project' ? this.defaultTopicId ?? '' : '',
        documentType: 'General',
        title: '',
        summary: '',
        content: '',
        sourceUrl: '',
        ticketUrl: '',
        changeNote: '',
        categoryId: '',
        status: 'Draft',
        tagIds: [],
        tagNames: '',
      });
      return;
    }

    this.form.reset({
      scope: this.item.scope,
      projectId: this.item.projectId ?? '',
      topicId: this.item.topicId ?? '',
      documentType: this.item.documentType,
      title: this.item.title,
      summary: this.item.summary ?? '',
      content: this.item.content,
      sourceUrl: this.item.sourceUrl ?? '',
      ticketUrl: this.item.ticketUrl ?? '',
      changeNote: this.item.changeNote ?? '',
      categoryId: this.item.category?.id ?? '',
      status: this.item.status,
      tagIds: this.item.tags.map((tag) => tag.id),
      tagNames: '',
    });
  }

  onProjectChange(): void {
    const projectId = this.form.controls.projectId.value;
    this.form.controls.topicId.setValue('');
    if (projectId) {
      this.projectSelected.emit(projectId);
    }
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const isProject = value.scope === 'Project';

    this.saveItem.emit({
      scope: value.scope,
      topicId: isProject ? value.topicId || null : null,
      documentType: value.documentType,
      title: value.title,
      content: value.content,
      summary: value.summary || null,
      sourceUrl: value.sourceUrl || null,
      ticketUrl: value.ticketUrl || null,
      changeNote: value.changeNote || null,
      categoryId: value.categoryId || null,
      status: value.status,
      tagIds: value.tagIds,
      tagNames: value.tagNames
        .split(',')
        .map((name) => name.trim())
        .filter(Boolean),
      expectedRevisionNumber: this.item ? this.item.currentRevisionNumber : undefined,
    });
  }

  isTagSelected(tagId: string): boolean {
    return this.form.controls.tagIds.value.includes(tagId);
  }

  toggleTag(tagId: string): void {
    const current = this.form.controls.tagIds.value;
    const next = current.includes(tagId)
      ? current.filter((id) => id !== tagId)
      : [...current, tagId];

    this.form.controls.tagIds.setValue(next);
    this.form.controls.tagIds.markAsDirty();
  }
}
