import { DocumentScope } from '../models/knowledge.models';

export const PROJECT_DOCUMENTS_HOME = '/project-documents';
export const PERSONAL_DOCUMENTS_HOME = '/knowledge';

export function documentsHomeCommands(scope: DocumentScope): string[] {
  return [scope === 'Project' ? PROJECT_DOCUMENTS_HOME : PERSONAL_DOCUMENTS_HOME];
}

export function documentDetailCommands(scope: DocumentScope, documentId: string): string[] {
  return [
    scope === 'Project' ? `${PROJECT_DOCUMENTS_HOME}/detail` : `${PERSONAL_DOCUMENTS_HOME}/detail`,
    documentId,
  ];
}

export function folderBrowseCommands(scope: DocumentScope, folderId: string): string[] {
  return [
    scope === 'Project' ? `${PROJECT_DOCUMENTS_HOME}/folder` : `${PERSONAL_DOCUMENTS_HOME}/folder`,
    folderId,
  ];
}

export function projectBrowseCommands(projectId: string): string[] {
  return [`${PROJECT_DOCUMENTS_HOME}/project`, projectId];
}

export function locationCommands(options: {
  scope: DocumentScope;
  projectId?: string | null;
  folderId?: string | null;
}): string[] {
  if (options.folderId) {
    return folderBrowseCommands(options.scope, options.folderId);
  }

  if (options.scope === 'Project' && options.projectId) {
    return projectBrowseCommands(options.projectId);
  }

  return documentsHomeCommands(options.scope);
}
