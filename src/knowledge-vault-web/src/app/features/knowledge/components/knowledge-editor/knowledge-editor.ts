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
  private static readonly maxContentImportBytes = 5 * 1024 * 1024;
  private static readonly textFileExtensions = new Set([
    'csv', 'html', 'htm', 'json', 'log', 'md', 'markdown', 'txt', 'xml', 'yaml', 'yml',
  ]);
  @Input() item: KnowledgeItem | null = null;
  @Input() categories: Category[] = [];
  @Input() tags: Tag[] = [];
  @Input() projects: ProjectSummary[] = [];
  @Input() topics: ProjectTopic[] = [];
  @Input() saving = false;
  @Input() workspaceScope: DocumentScope = 'Personal';
  @Input() defaultProjectId: string | null = null;
  @Input() defaultTopicId: string | null = null;
  @Input() defaultFolderId: string | null = null;
  /** Display name of the parent folder a new document will be created in.
   *  Resolved by id on the caller side so it stays accurate after renames.
   *  Null when there is no parent (root level). */
  @Input() parentFolderName: string | null = null;

  @Output() saveItem = new EventEmitter<SaveDocumentRequest>();
  @Output() deleteItem = new EventEmitter<void>();
  @Output() closeDialog = new EventEmitter<void>();
  @Output() projectSelected = new EventEmitter<string>();

  readonly statuses: KnowledgeItemStatus[] = ['Draft', 'Active', 'Archived'];
  contentDropActive = false;
  contentImportMessage: string | null = null;

  readonly form = new FormBuilder().nonNullable.group({
    projectId: [''],
    topicId: [''],
    documentType: ['General' as DocumentType, [Validators.required]],
    title: ['', [Validators.required, Validators.maxLength(256)]],
    summary: [''],
    content: ['', [Validators.required]],
    linkDisplayText: [''],
    linkUrl: [''],
    categoryId: [''],
    status: ['Draft' as KnowledgeItemStatus, [Validators.required]],
    tagIds: [[] as string[]],
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

    this.configureProjectValidation();

    if (!this.item) {
      this.form.reset({
        projectId: this.workspaceScope === 'Project' ? (this.defaultProjectId ?? '') : '',
        topicId: this.workspaceScope === 'Project' ? (this.defaultTopicId ?? '') : '',
        documentType: 'General',
        title: '',
        summary: '',
        content: '',
        linkDisplayText: '',
        linkUrl: '',
        categoryId: '',
        status: 'Draft',
        tagIds: [],
      });
      this.configureSystemDocumentControls();
      return;
    }

    this.form.reset({
      projectId: this.item.projectId ?? '',
      topicId: this.item.topicId ?? '',
      documentType: this.item.documentType,
      title: this.item.title,
      summary: this.item.summary ?? '',
      content: this.item.content,
      linkDisplayText: this.item.linkDisplayText ?? '',
      linkUrl: this.item.linkUrl ?? '',
      categoryId: this.item.category?.id ?? '',
      status: this.item.status,
      tagIds: this.item.tags.map((tag) => tag.id),
    });
    this.configureSystemDocumentControls();
  }

  onProjectChange(): void {
    const projectId = this.form.controls.projectId.value;
    this.form.controls.topicId.setValue('');
    if (projectId) {
      this.projectSelected.emit(projectId);
    }
  }

  submit(): void {
    if (this.isProjectMemory() || this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const isProject = this.workspaceScope === 'Project';

    this.saveItem.emit({
      scope: this.workspaceScope,
      projectId: isProject ? value.projectId || null : null,
      topicId: isProject ? value.topicId || null : null,
      documentType: value.documentType,
      title: value.title,
      content: value.content,
      summary: value.summary || null,
      sourceUrl: this.item?.sourceUrl ?? null,
      linkDisplayText: value.linkDisplayText || null,
      linkUrl: value.linkUrl || null,
      changeNote: null,
      categoryId: value.categoryId || null,
      status: value.status,
      tagIds: value.tagIds,
      tagNames: [],
      folderId: this.defaultFolderId ?? null,
      expectedRevisionNumber: this.item ? this.item.currentRevisionNumber : undefined,
    });
  }

  onContentDragOver(event: DragEvent): void {
    if (this.isProjectMemory() || !event.dataTransfer?.files.length) {
      return;
    }
    event.preventDefault();
    event.dataTransfer.dropEffect = 'copy';
  }

  onContentDragEnter(event: DragEvent): void {
    if (this.isProjectMemory() || !event.dataTransfer?.files.length) {
      return;
    }
    event.preventDefault();
    this.contentDropActive = true;
    this.contentImportMessage = null;
  }

  onContentDragLeave(): void {
    this.contentDropActive = false;
  }

  async onContentDrop(event: DragEvent): Promise<void> {
    event.preventDefault();
    this.contentDropActive = false;
    if (this.isProjectMemory()) {
      return;
    }

    const file = event.dataTransfer?.files.item(0);
    if (!file) {
      return;
    }
    if (!this.isTextFile(file)) {
      this.contentImportMessage = 'Drop a text file such as .md, .txt, .json, or .csv.';
      return;
    }
    if (file.size > KnowledgeEditor.maxContentImportBytes) {
      this.contentImportMessage = 'The file must be 5 MB or smaller.';
      return;
    }

    try {
      const content = await file.text();
      this.form.controls.content.setValue(content);
      this.form.controls.content.markAsDirty();
      this.form.controls.content.markAsTouched();
      this.form.controls.content.updateValueAndValidity();
      if (!this.form.controls.title.value.trim()) {
        this.form.controls.title.setValue(file.name);
        this.form.controls.title.markAsDirty();
      }
      this.contentImportMessage = `Imported ${file.name}.`;
    } catch {
      this.contentImportMessage = `Couldn't read ${file.name}.`;
    }
  }

  private configureProjectValidation(): void {
    const projectControl = this.form.controls.projectId;
    if (this.workspaceScope === 'Project') {
      projectControl.setValidators([Validators.required]);
    } else {
      projectControl.clearValidators();
    }
    projectControl.updateValueAndValidity({ emitEvent: false });
  }

  isTagSelected(tagId: string): boolean {
    return this.form.controls.tagIds.value.includes(tagId);
  }

  toggleTag(tagId: string): void {
    if (this.isProjectMemory()) {
      return;
    }

    const current = this.form.controls.tagIds.value;
    const next = current.includes(tagId)
      ? current.filter((id) => id !== tagId)
      : [...current, tagId];

    this.form.controls.tagIds.setValue(next);
    this.form.controls.tagIds.markAsDirty();
  }

  isProjectMemory(): boolean {
    return this.item?.documentType === 'ProjectMemory';
  }

  private isTextFile(file: File): boolean {
    if (file.type.startsWith('text/') || file.type === 'application/json') {
      return true;
    }
    const extension = file.name.split('.').pop()?.toLowerCase();
    return extension !== undefined && KnowledgeEditor.textFileExtensions.has(extension);
  }

  private configureSystemDocumentControls(): void {
    const controls = [
      this.form.controls.projectId,
      this.form.controls.topicId,
      this.form.controls.documentType,
      this.form.controls.title,
      this.form.controls.summary,
      this.form.controls.content,
      this.form.controls.linkDisplayText,
      this.form.controls.linkUrl,
      this.form.controls.categoryId,
      this.form.controls.status,
      this.form.controls.tagIds,
    ];

    for (const control of controls) {
      if (this.isProjectMemory()) {
        control.disable({ emitEvent: false });
      } else {
        control.enable({ emitEvent: false });
      }
    }
  }
}
