import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';

import { ApiClient } from '../../../core/api/api-client.service';
import { AuthService } from '../../../core/auth/auth.service';
import { getErrorMessage } from '../../../core/http/error-message';
import { KnowledgeItem } from '../../../core/models/knowledge.models';
import {
  Project,
  ProjectGroup,
  ProjectMember,
  ProjectMemoryCandidate,
  ProjectMemorySection,
} from '../../../core/models/projects.models';
import { LoadingIndicator } from '../../../shared/components/loading-indicator/loading-indicator';

@Component({
  selector: 'app-project-detail-page',
  imports: [DatePipe, ReactiveFormsModule, RouterLink, LoadingIndicator],
  templateUrl: './project-detail-page.html',
  styleUrl: './project-detail-page.css',
})
export class ProjectDetailPage {
  private readonly api = inject(ApiClient);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);
  private readonly projectId = this.route.snapshot.paramMap.get('id');

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly followBusy = signal(false);
  readonly candidateSaving = signal(false);
  readonly reviewingCandidateId = signal<string | null>(null);
  readonly memberBusyId = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly project = signal<Project | null>(null);
  readonly memory = signal<KnowledgeItem | null>(null);
  readonly memoryCandidates = signal<ProjectMemoryCandidate[]>([]);
  readonly groups = signal<ProjectGroup[]>([]);
  readonly showCreateGroup = signal(false);
  readonly showCandidateForm = signal(false);
  readonly includeResolvedCandidates = signal(false);
  readonly memorySections: ReadonlyArray<{ value: ProjectMemorySection; label: string }> = [
    { value: 'ProjectPurpose', label: 'Project purpose' },
    { value: 'CurrentContext', label: 'Current context' },
    { value: 'ConstraintsAndConventions', label: 'Constraints and conventions' },
    { value: 'KeyDecisions', label: 'Key decisions' },
    { value: 'ImportantLocationsAndCommands', label: 'Important locations and commands' },
    { value: 'AgentPrompts', label: 'Agent prompts' },
    { value: 'AgentHandoff', label: 'Agent handoff' },
    { value: 'OpenQuestions', label: 'Open questions' },
  ];
  readonly canManageGroups = computed(() => {
    const role = this.project()?.currentUserRole;
    return role === 'Owner' || role === 'Admin' || role === 'Editor';
  });
  readonly canReviewMemory = computed(() => {
    const role = this.project()?.currentUserRole;
    return role === 'Owner' || role === 'Admin';
  });
  readonly canManageMembers = this.canReviewMemory;

  readonly groupForm = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(128)]],
    description: ['', [Validators.maxLength(512)]],
  });

  readonly candidateForm = this.fb.nonNullable.group({
    targetSection: ['AgentPrompts' as ProjectMemorySection, [Validators.required]],
    proposedContent: ['', [Validators.required, Validators.maxLength(16000)]],
    rationale: ['', [Validators.maxLength(1024)]],
  });

  constructor() {
    if (!this.projectId) {
      this.error.set('Project id is missing.');
      this.loading.set(false);
      return;
    }

    this.loadProject();
  }

  loadProject(): void {
    if (!this.projectId) {
      return;
    }

    this.loading.set(true);
    this.api.getProject(this.projectId).subscribe({
      next: (project) => {
        this.project.set(project);
        if (project.isFollowing) {
          this.loadWorkspace();
        } else {
          this.groups.set([]);
          this.memory.set(null);
          this.memoryCandidates.set([]);
          this.loading.set(false);
        }
      },
      error: (error) => {
        this.error.set(getErrorMessage(error));
        this.loading.set(false);
      },
    });
  }

  loadWorkspace(): void {
    if (!this.projectId) {
      return;
    }

    forkJoin({
      groups: this.api.listGroups(this.projectId),
      memory: this.api.getProjectMemory(this.projectId),
      candidates: this.api.listProjectMemoryCandidates(
        this.projectId,
        this.includeResolvedCandidates(),
      ),
    }).subscribe({
      next: ({ groups, memory, candidates }) => {
        this.groups.set(groups.items);
        this.memory.set(memory);
        this.memoryCandidates.set(candidates);
      },
      error: (error) => {
        this.error.set(getErrorMessage(error));
        this.loading.set(false);
      },
      complete: () => this.loading.set(false),
    });
  }

  toggleFollow(): void {
    const project = this.project();
    if (!project || project.currentUserRole === 'Owner') {
      return;
    }

    this.followBusy.set(true);
    const operation = project.isFollowing
      ? this.api.unfollowProject(project.id)
      : this.api.followProject(project.id);
    operation.subscribe({
      next: () => this.loadProject(),
      error: (error) => {
        this.error.set(getErrorMessage(error));
        this.followBusy.set(false);
      },
      complete: () => this.followBusy.set(false),
    });
  }

  toggleCreateGroup(): void {
    this.showCreateGroup.update((value) => !value);
    this.groupForm.reset({ name: '', description: '' });
  }

  toggleCandidateForm(): void {
    this.showCandidateForm.update((value) => !value);
    this.candidateForm.reset({
      targetSection: 'AgentPrompts',
      proposedContent: '',
      rationale: '',
    });
  }

  submitMemoryCandidate(): void {
    const project = this.project();
    if (!project || this.candidateForm.invalid) {
      this.candidateForm.markAllAsTouched();
      return;
    }

    const value = this.candidateForm.getRawValue();
    this.candidateSaving.set(true);
    this.error.set(null);
    this.api.createProjectMemoryCandidate(project.id, {
      targetSection: value.targetSection,
      proposedContent: value.proposedContent.trim(),
      rationale: value.rationale.trim() || null,
    }).subscribe({
      next: () => {
        this.showCandidateForm.set(false);
        this.loadWorkspace();
      },
      error: (error) => {
        this.error.set(getErrorMessage(error));
        this.candidateSaving.set(false);
      },
      complete: () => this.candidateSaving.set(false),
    });
  }

  reviewMemoryCandidate(candidateId: string, action: 'accept' | 'cancel'): void {
    const project = this.project();
    if (!project || !this.canReviewMemory()) {
      return;
    }

    this.reviewingCandidateId.set(candidateId);
    this.error.set(null);
    const operation = action === 'accept'
      ? this.api.acceptProjectMemoryCandidate(project.id, candidateId)
      : this.api.cancelProjectMemoryCandidate(project.id, candidateId);
    operation.subscribe({
      next: () => this.loadWorkspace(),
      error: (error) => {
        this.error.set(getErrorMessage(error));
        this.reviewingCandidateId.set(null);
      },
      complete: () => this.reviewingCandidateId.set(null),
    });
  }

  toggleResolvedCandidates(): void {
    this.includeResolvedCandidates.update((value) => !value);
    this.loadWorkspace();
  }

  toggleAdministrator(member: ProjectMember): void {
    const project = this.project();
    if (!project || !this.canManageMembers() || member.role === 'Owner' || this.isCurrentUser(member)) {
      return;
    }

    this.memberBusyId.set(member.userId);
    this.error.set(null);
    this.api.updateProjectMemberRole(
      project.id,
      member.userId,
      member.role === 'Admin' ? 'Editor' : 'Admin',
    ).subscribe({
      next: () => this.loadProject(),
      error: (error) => {
        this.error.set(getErrorMessage(error));
        this.memberBusyId.set(null);
      },
      complete: () => this.memberBusyId.set(null),
    });
  }

  isCurrentUser(member: ProjectMember): boolean {
    return member.userId === this.auth.currentUser()?.id;
  }

  memorySectionLabel(section: ProjectMemorySection): string {
    return this.memorySections.find((item) => item.value === section)?.label ?? section;
  }

  createGroup(): void {
    const project = this.project();
    if (!project || this.groupForm.invalid) {
      this.groupForm.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const value = this.groupForm.getRawValue();
    this.api.createGroup(project.id, {
      name: value.name.trim(),
      description: value.description.trim() || null,
    }).subscribe({
      next: () => {
        this.showCreateGroup.set(false);
        this.loadWorkspace();
      },
      error: (error) => {
        this.error.set(getErrorMessage(error));
        this.saving.set(false);
      },
      complete: () => this.saving.set(false),
    });
  }

  deleteGroup(groupId: string): void {
    const project = this.project();
    if (!project || !this.canReviewMemory()) {
      return;
    }

    this.api.deleteGroup(project.id, groupId).subscribe({
      next: () => this.loadWorkspace(),
      error: (error) => this.error.set(getErrorMessage(error)),
    });
  }
}
