export function fileNameWithoutLastExtension(fileName: string): string {
  const lastDot = fileName.lastIndexOf('.');
  if (lastDot <= 0) {
    return fileName;
  }

  return fileName.slice(0, lastDot);
}

export function importTitlesEqual(left: string, right: string): boolean {
  return left.localeCompare(right, undefined, { sensitivity: 'accent' }) === 0;
}

export function findExistingImportDocument<T extends { title: string }>(
  documents: readonly T[],
  fileName: string,
): T | undefined {
  const exact = documents.filter((document) => importTitlesEqual(document.title, fileName));
  if (exact.length === 1) {
    return exact[0];
  }

  if (exact.length > 1) {
    return undefined;
  }

  const stem = fileNameWithoutLastExtension(fileName);
  const sameStemFamily = documents.filter((document) =>
    importTitlesEqual(document.title, stem)
    || importTitlesEqual(fileNameWithoutLastExtension(document.title), stem));

  if (sameStemFamily.length > 1) {
    return undefined;
  }

  return sameStemFamily.find((document) => importTitlesEqual(document.title, stem));
}
