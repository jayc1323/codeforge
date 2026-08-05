import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export const STATUS_LABELS: Record<number, string> = {
  0: 'Queued',
  1: 'Running',
  2: 'Completed',
  3: 'Failed',
  4: 'Timed out',
  5: 'Compile error'
};

export interface Language {
  id: string;
  displayName: string;
  version: string;
  docsUrl: string;
}

export interface SubmitExecutionRequest {
  language: string;
  sourceCode: string;
  standardInput?: string;
}

export interface Execution {
  id: string;
  language: string;
  status: number;
  sourceCode: string | null;
  standardInput: string | null;
  stdout: string | null;
  stderr: string | null;
  exitCode: number | null;
  durationMs: number | null;
  createdAt: string;
  completedAt: string | null;
}

export interface ExecutionHistoryItem {
  id: string;
  language: string;
  status: number;
  exitCode: number | null;
  durationMs: number | null;
  createdAt: string;
  completedAt: string | null;
  sourcePreview: string;
}

@Injectable({ providedIn: 'root' })
export class ExecutionService {
  private http = inject(HttpClient);

  getLanguages(): Observable<Language[]> {
    return this.http.get<Language[]>('/api/languages');
  }

  submit(request: SubmitExecutionRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>('/api/executions', request);
  }

  getExecution(id: string): Observable<Execution> {
    return this.http.get<Execution>(`/api/executions/${id}`);
  }

  getHistory(take = 50): Observable<ExecutionHistoryItem[]> {
    return this.http.get<ExecutionHistoryItem[]>(`/api/executions/mine?take=${take}`);
  }
}
