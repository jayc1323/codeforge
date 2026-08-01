import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';

export interface ExecutionEventHandlers {
  onStatus(status: number): void;
  onOutput(stream: 'stdout' | 'stderr', chunk: string): void;
  onCompleted(result: {
    status: number;
    stdout: string | null;
    stderr: string | null;
    exitCode: number | null;
    durationMs: number | null;
  }): void;
  onError(): void;
}

@Injectable({ providedIn: 'root' })
export class ExecutionStreamService {
  private connection: signalR.HubConnection | null = null;

  private async ensureConnected(): Promise<signalR.HubConnection> {
    if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
      return this.connection;
    }

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/executions')
      .withAutomaticReconnect()
      .build();

    await this.connection.start();
    return this.connection;
  }

  async watchExecution(executionId: string, handlers: ExecutionEventHandlers): Promise<void> {
    const connection = await this.ensureConnected();

    connection.off('status');
    connection.off('output');
    connection.off('completed');

    connection.on('status', (event: { executionId: string; status: number }) => {
      if (event.executionId === executionId) handlers.onStatus(event.status);
    });
    connection.on('output', (event: { executionId: string; stream: 'stdout' | 'stderr'; chunk: string }) => {
      if (event.executionId === executionId) handlers.onOutput(event.stream, event.chunk);
    });
    connection.on('completed', (event: {
      executionId: string; status: number; stdout: string | null;
      stderr: string | null; exitCode: number | null; durationMs: number | null;
    }) => {
      if (event.executionId === executionId) handlers.onCompleted(event);
    });
    connection.onreconnecting(() => handlers.onError());

    await connection.invoke('WatchExecution', executionId);
  }
}
