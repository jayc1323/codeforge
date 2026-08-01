/**
 * Minimal Language Server Protocol client.
 * Speaks JSON-RPC over a WebSocket to our backend bridge (/lsp/{language}),
 * which forwards to the language server's stdio.
 *
 * Supports: initialize handshake, didOpen/didChange/didClose sync (full document),
 * completion + hover requests, publishDiagnostics notifications.
 */
export interface LspPosition {
  line: number;
  character: number;
}

export interface LspDiagnostic {
  range: { start: LspPosition; end: LspPosition };
  severity?: number; // 1 Error, 2 Warning, 3 Info, 4 Hint
  message: string;
  source?: string;
}

export class LspClient {
  private ws: WebSocket | null = null;
  private seq = 0;
  private version = 0;
  private pending = new Map<number, { resolve: (v: any) => void; reject: (e: any) => void }>();

  onDiagnostics: ((uri: string, diagnostics: LspDiagnostic[]) => void) | null = null;
  onExit: (() => void) | null = null;

  constructor(
    private readonly language: string,
    private readonly languageId: string,
    private readonly documentUri: string
  ) {}

  async connect(): Promise<void> {
    const protocol = location.protocol === 'https:' ? 'wss' : 'ws';
    this.ws = new WebSocket(`${protocol}://${location.host}/lsp/${this.language}`);

    await new Promise<void>((resolve, reject) => {
      this.ws!.onopen = () => resolve();
      this.ws!.onerror = () => reject(new Error('LSP WebSocket connection failed'));
    });

    this.ws.onmessage = (event) => this.handleMessage(JSON.parse(event.data));
    this.ws.onclose = () => this.onExit?.();

    await this.request('initialize', {
      processId: null,
      rootUri: null,
      clientInfo: { name: 'codeforge' },
      capabilities: {
        textDocument: {
          synchronization: { didSave: false, dynamicRegistration: false },
          completion: { completionItem: { snippetSupport: false, documentationFormat: ['plaintext'] } },
          hover: { contentFormat: ['plaintext'] },
          publishDiagnostics: {}
        }
      }
    });
    this.notify('initialized', {});
    this.didOpen(this.getText());
  }

  // Set by the host so didOpen can send the initial document content.
  getText: () => string = () => '';

  didOpen(text: string): void {
    this.notify('textDocument/didOpen', {
      textDocument: { uri: this.documentUri, languageId: this.languageId, version: ++this.version, text }
    });
  }

  didChange(text: string): void {
    this.notify('textDocument/didChange', {
      textDocument: { uri: this.documentUri, version: ++this.version },
      contentChanges: [{ text }]
    });
  }

  completion(position: LspPosition): Promise<any> {
    return this.request('textDocument/completion', {
      textDocument: { uri: this.documentUri },
      position
    });
  }

  hover(position: LspPosition): Promise<any> {
    return this.request('textDocument/hover', {
      textDocument: { uri: this.documentUri },
      position
    });
  }

  async dispose(): Promise<void> {
    try {
      this.notify('textDocument/didClose', { textDocument: { uri: this.documentUri } });
      await this.request('shutdown');
      this.notify('exit');
    } catch { /* best effort */ }
    this.ws?.close();
    this.ws = null;
  }

  private request(method: string, params: unknown = null): Promise<any> {
    const id = ++this.seq;
    return new Promise((resolve, reject) => {
      this.pending.set(id, { resolve, reject });
      this.send({ jsonrpc: '2.0', id, method, params });
    });
  }

  private notify(method: string, params: unknown = null): void {
    this.send({ jsonrpc: '2.0', method, params });
  }

  private respond(id: number, result: unknown): void {
    this.send({ jsonrpc: '2.0', id, result });
  }

  private send(message: unknown): void {
    if (this.ws?.readyState === WebSocket.OPEN) {
      this.ws.send(JSON.stringify(message));
    }
  }

  private handleMessage(message: any): void {
    if (message.method && message.id !== undefined) {
      // Server -> client request: acknowledge with benign defaults.
      if (message.method === 'workspace/configuration') {
        this.respond(message.id, (message.params?.items ?? []).map(() => ({})));
      } else {
        this.respond(message.id, null);
      }
      return;
    }

    if (message.id !== undefined) {
      const entry = this.pending.get(message.id);
      if (entry) {
        this.pending.delete(message.id);
        if (message.error) entry.reject(message.error);
        else entry.resolve(message.result);
      }
      return;
    }

    if (message.method === 'textDocument/publishDiagnostics') {
      this.onDiagnostics?.(message.params.uri, message.params.diagnostics ?? []);
    }
  }
}
