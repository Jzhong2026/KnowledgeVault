import {
  documentDetailCommands,
  documentsHomeCommands,
  folderBrowseCommands,
  locationCommands,
  projectBrowseCommands,
} from './document-location.routes';

describe('document location routes', () => {
  it('builds document detail paths without query strings', () => {
    expect(documentDetailCommands('Project', 'doc-id')).toEqual([
      '/project-documents/detail',
      'doc-id',
    ]);
    expect(documentDetailCommands('Personal', 'doc-id')).toEqual([
      '/knowledge/detail',
      'doc-id',
    ]);
  });

  it('builds folder browse paths without query strings', () => {
    expect(folderBrowseCommands('Project', 'folder-id')).toEqual([
      '/project-documents/folder',
      'folder-id',
    ]);
    expect(folderBrowseCommands('Personal', 'folder-id')).toEqual([
      '/knowledge/folder',
      'folder-id',
    ]);
  });

  it('builds a project root path without query strings', () => {
    expect(projectBrowseCommands('project-id')).toEqual([
      '/project-documents/project',
      'project-id',
    ]);
  });

  it('chooses home, project root, or folder from the current location', () => {
    expect(locationCommands({ scope: 'Personal' })).toEqual(['/knowledge']);
    expect(locationCommands({ scope: 'Project' })).toEqual(['/project-documents']);
    expect(locationCommands({ scope: 'Project', projectId: 'project-id' })).toEqual([
      '/project-documents/project',
      'project-id',
    ]);
    expect(
      locationCommands({
        scope: 'Project',
        projectId: 'project-id',
        folderId: 'folder-id',
      }),
    ).toEqual(['/project-documents/folder', 'folder-id']);
    expect(locationCommands({ scope: 'Personal', folderId: 'folder-id' })).toEqual([
      '/knowledge/folder',
      'folder-id',
    ]);
  });

  it('returns the documents home command', () => {
    expect(documentsHomeCommands('Project')).toEqual(['/project-documents']);
    expect(documentsHomeCommands('Personal')).toEqual(['/knowledge']);
  });
});
