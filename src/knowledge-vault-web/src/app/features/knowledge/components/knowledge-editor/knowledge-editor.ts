import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import {
  Category,
  KnowledgeItem,
  KnowledgeItemStatus,
  SaveKnowledgeItemRequest,
  Tag,
} from '../../../../core/models/knowledge.models';

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
  @Input() saving = false;

  @Output() saveItem = new EventEmitter<SaveKnowledgeItemRequest>();
  @Output() deleteItem = new EventEmitter<void>();
  @Output() createNew = new EventEmitter<void>();
  @Output() closeDialog = new EventEmitter<void>();

  readonly statuses: KnowledgeItemStatus[] = ['Draft', 'Active', 'Archived'];

  readonly form = new FormBuilder().nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(256)]],
    summary: [''],
    content: ['', [Validators.required]],
    sourceUrl: [''],
    categoryId: [''],
    status: ['Draft' as KnowledgeItemStatus, [Validators.required]],
    tagIds: [[] as string[]],
    tagNames: [''],
  });

  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['item']) {
      return;
    }

    if (!this.item) {
      this.form.reset({
        title: '',
        summary: '',
        content: '',
        sourceUrl: '',
        categoryId: '',
        status: 'Draft',
        tagIds: [],
        tagNames: '',
      });
      return;
    }

    this.form.reset({
      title: this.item.title,
      summary: this.item.summary ?? '',
      content: this.item.content,
      sourceUrl: this.item.sourceUrl ?? '',
      categoryId: this.item.category?.id ?? '',
      status: this.item.status,
      tagIds: this.item.tags.map((tag) => tag.id),
      tagNames: '',
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.saveItem.emit({
      title: value.title,
      summary: value.summary || null,
      content: value.content,
      sourceUrl: value.sourceUrl || null,
      categoryId: value.categoryId || null,
      status: value.status,
      tagIds: value.tagIds,
      tagNames: value.tagNames
        .split(',')
        .map((name) => name.trim())
        .filter(Boolean),
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
