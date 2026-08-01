import { Component, ElementRef, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Execution, ExecutionService, Language, STATUS_LABELS } from '../services/execution.service';
import { ExecutionStreamService } from '../services/execution-stream.service';
import { MonacoLoaderService } from '../services/monaco-loader.service';

const MONACO_LANGUAGE: Record<string, string> = {
  python: 'python',
  cpp: 'cpp',
  csharp: 'csharp',
  fsharp: 'fsharp',
  typescript: 'typescript'
};

const SAMPLES: Record<string, string> = {
  python: 'name = input("What is your name? ")\nprint(f"Hello, {name}!")\n\nfor i in range(5):\n    print(i * i)\n',
  cpp: '#include <iostream>\n#include <string>\n\nint main() {\n    std::string name;\n    std::getline(std::cin, name);\n    std::cout << "Hello, " << name << "!" << std::endl;\n    return 0;\n}\n',
  csharp: 'Console.WriteLine("Hello from C#!");\nvar squares = Enumerable.Range(1, 5).Select(x => x * x);\nConsole.WriteLine(string.Join(", ", squares));\n',
  fsharp: 'printfn "Hello from F#!"\n\n[1..5]\n|> List.map (fun x -> x * x)\n|> printfn "%A"\n',
  typescript: 'interface User {\n  name: string;\n  age: number;\n}\n\nfunction greet(user: User): string {\n  return `Hello, ${user.name} (${user.age})`;\n}\n\nconsole.log(greet({ name: "Ada", age: 36 }));\nconsole.log([1, 2, 3, 4, 5].map((x) => x * x).join(", "));\n'
};

const THEME_STORAGE_KEY = 'codeforge-theme';

@Component({
  selector: 'app-editor',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './editor.component.html',
  styleUrl: './editor.component.css',
  host: { '[class.light]': '!isDarkTheme' }
})
export class EditorComponent implements OnInit, OnDestroy {
  @ViewChild('editorContainer', { static: true }) editorContainer!: ElementRef;

  private executionService = inject(ExecutionService);
  private streamService = inject(ExecutionStreamService);
  private monacoLoader = inject(MonacoLoaderService);
  private editor: any;
  private monaco: any;
  private pollTimer: any;

  languages: Language[] = [];
  selectedLanguage = 'python';
  standardInput = '';
  running = false;
  execution: Execution | null = null;
  errorMessage = '';
  isDarkTheme = localStorage.getItem(THEME_STORAGE_KEY) !== 'light';

  readonly statusLabels = STATUS_LABELS;

  get selectedDocsUrl(): string | null {
    return this.languages.find(l => l.id === this.selectedLanguage)?.docsUrl ?? null;
  }

  async ngOnInit(): Promise<void> {
    this.executionService.getLanguages().subscribe({
      next: (languages) => (this.languages = languages),
      error: () => (this.errorMessage = 'Could not reach the CodeForge API.')
    });

    this.monaco = await this.monacoLoader.load();
    this.editor = this.monaco.editor.create(this.editorContainer.nativeElement, {
      value: SAMPLES[this.selectedLanguage],
      language: MONACO_LANGUAGE[this.selectedLanguage],
      theme: this.monacoTheme,
      automaticLayout: true,
      minimap: { enabled: false },
      fontSize: 14
    });
  }

  get monacoTheme(): string {
    return this.isDarkTheme ? 'vs-dark' : 'vs';
  }

  toggleTheme(): void {
    this.isDarkTheme = !this.isDarkTheme;
    localStorage.setItem(THEME_STORAGE_KEY, this.isDarkTheme ? 'dark' : 'light');
    this.monaco?.editor.setTheme(this.monacoTheme);
  }

  ngOnDestroy(): void {
    this.editor?.dispose();
    clearInterval(this.pollTimer);
  }

  onLanguageChange(): void {
    const model = this.editor?.getModel();
    if (model) {
      this.monaco.editor.setModelLanguage(model, MONACO_LANGUAGE[this.selectedLanguage] ?? 'plaintext');
      model.setValue(SAMPLES[this.selectedLanguage] ?? '');
    }
  }

  run(): void {
    if (this.running) return;
    this.running = true;
    this.execution = null;
    this.errorMessage = '';

    this.executionService.submit({
      language: this.selectedLanguage,
      sourceCode: this.editor.getValue(),
      standardInput: this.standardInput || undefined
    }).subscribe({
      next: (response) => this.streamExecution(response.id),
      error: (err) => {
        this.running = false;
        this.errorMessage = err.error?.error ?? 'Submission failed.';
      }
    });
  }

  private streamExecution(id: string): void {
    clearInterval(this.pollTimer);
    // Live view that grows as stdout/stderr chunks arrive over SignalR.
    this.execution = {
      id, language: this.selectedLanguage, status: 1,
      stdout: '', stderr: '', exitCode: null, durationMs: null,
      createdAt: new Date().toISOString(), completedAt: null
    };

    this.streamService.watchExecution(id, {
      onStatus: (status) => {
        if (this.execution) this.execution.status = status;
      },
      onOutput: (stream, chunk) => {
        if (!this.execution) return;
        if (stream === 'stdout') this.execution.stdout = (this.execution.stdout ?? '') + chunk;
        else this.execution.stderr = (this.execution.stderr ?? '') + chunk;
      },
      onCompleted: (result) => {
        if (this.execution) {
          this.execution.status = result.status;
          this.execution.exitCode = result.exitCode;
          this.execution.durationMs = result.durationMs;
        }
        this.running = false;
      },
      onError: () => {}
    }).catch(() => {
      // SignalR unavailable -> fall back to polling.
      this.execution = null;
      this.pollExecution(id);
    });
  }

  private pollExecution(id: string): void {
    clearInterval(this.pollTimer);
    this.pollTimer = setInterval(() => {
      this.executionService.getExecution(id).subscribe({
        next: (execution) => {
          this.execution = execution;
          if (execution.status !== 0 && execution.status !== 1) {
            clearInterval(this.pollTimer);
            this.running = false;
          }
        },
        error: () => {
          clearInterval(this.pollTimer);
          this.running = false;
          this.errorMessage = 'Lost connection while polling the execution.';
        }
      });
    }, 500);
  }
}
