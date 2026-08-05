import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface SnippetSummary {
  id: string;
  title: string;
  language: string;
  updatedAt: string;
}

export interface Snippet {
  id: string;
  title: string;
  language: string;
  sourceCode: string;
  standardInput: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface SaveSnippetRequest {
  title: string;
  language: string;
  sourceCode: string;
  standardInput?: string;
}

@Injectable({ providedIn: 'root' })
export class SnippetService {
  private http = inject(HttpClient);

  list(): Observable<SnippetSummary[]> {
    return this.http.get<SnippetSummary[]>('/api/snippets');
  }

  get(id: string): Observable<Snippet> {
    return this.http.get<Snippet>(`/api/snippets/${id}`);
  }

  save(request: SaveSnippetRequest): Observable<Snippet> {
    return this.http.post<Snippet>('/api/snippets', request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`/api/snippets/${id}`);
  }
}
