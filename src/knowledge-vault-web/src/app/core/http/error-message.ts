import { HttpErrorResponse } from '@angular/common/http';

export function getErrorMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const problem = error.error as { detail?: string; title?: string } | null;
    return problem?.detail || problem?.title || `Request failed with status ${error.status}.`;
  }

  return 'Something went wrong. Please try again.';
}
