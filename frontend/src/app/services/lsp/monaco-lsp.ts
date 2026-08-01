import { LspClient } from './lsp-client';

const COMPLETION_KIND: Record<number, string> = {
  1: 'Text', 2: 'Method', 3: 'Function', 4: 'Constructor', 5: 'Field', 6: 'Variable',
  7: 'Class', 8: 'Interface', 9: 'Module', 10: 'Property', 11: 'Unit', 12: 'Value',
  13: 'Enum', 14: 'Keyword', 15: 'Snippet', 16: 'Color', 17: 'File', 18: 'Reference',
  19: 'Folder', 20: 'EnumMember', 21: 'Constant', 22: 'Struct', 23: 'Event',
  24: 'Operator', 25: 'TypeParameter'
};

function documentationToString(doc: any): string | undefined {
  if (!doc) return undefined;
  if (typeof doc === 'string') return doc;
  return doc.value ?? undefined;
}

/**
 * Wires an LspClient into a Monaco editor: completion, hover, diagnostics,
 * and document sync. Returns a dispose function.
 */
export function attachLsp(monaco: any, editor: any, client: LspClient, languageId: string): () => void {
  const model = editor.getModel();
  const documentUri = model.uri.toString();

  const completionProvider = monaco.languages.registerCompletionItemProvider(languageId, {
    triggerCharacters: ['.'],
    provideCompletionItems: async (m: any, position: any) => {
      if (m.uri.toString() !== documentUri) return { suggestions: [] };

      const result = await client.completion({
        line: position.lineNumber - 1,
        character: position.column - 1
      }).catch(() => null);

      const items = Array.isArray(result) ? result : result?.items ?? [];
      const word = m.getWordUntilPosition(position);
      const range = new monaco.Range(
        position.lineNumber, word.startColumn, position.lineNumber, word.endColumn);

      return {
        suggestions: items.map((item: any) => ({
          label: item.label,
          kind: monaco.languages.CompletionItemKind[COMPLETION_KIND[item.kind] ?? 'Text'],
          detail: item.detail,
          documentation: documentationToString(item.documentation),
          insertText: item.insertText ?? item.label,
          range
        }))
      };
    }
  });

  const hoverProvider = monaco.languages.registerHoverProvider(languageId, {
    provideHover: async (m: any, position: any) => {
      if (m.uri.toString() !== documentUri) return null;

      const result = await client.hover({
        line: position.lineNumber - 1,
        character: position.column - 1
      }).catch(() => null);

      if (!result?.contents) return null;
      const contents = result.contents;
      const value = typeof contents === 'string'
        ? contents
        : contents.value ?? (Array.isArray(contents)
            ? contents.map((c: any) => c.value ?? c).join('\n')
            : '');

      return value ? { contents: [{ value }] } : null;
    }
  });

  client.onDiagnostics = (_uri, diagnostics) => {
    monaco.editor.setModelMarkers(model, 'lsp', diagnostics.map((d) => ({
      severity: d.severity === 1
        ? monaco.MarkerSeverity.Error
        : d.severity === 2 ? monaco.MarkerSeverity.Warning : monaco.MarkerSeverity.Info,
      message: d.message,
      startLineNumber: d.range.start.line + 1,
      startColumn: d.range.start.character + 1,
      endLineNumber: d.range.end.line + 1,
      endColumn: d.range.end.character + 1
    })));
  };

  let changeTimer: ReturnType<typeof setTimeout>;
  const contentSubscription = model.onDidChangeContent(() => {
    clearTimeout(changeTimer);
    changeTimer = setTimeout(() => client.didChange(model.getValue()), 300);
  });

  return () => {
    clearTimeout(changeTimer);
    contentSubscription.dispose();
    completionProvider.dispose();
    hoverProvider.dispose();
    monaco.editor.setModelMarkers(model, 'lsp', []);
    client.dispose();
  };
}
