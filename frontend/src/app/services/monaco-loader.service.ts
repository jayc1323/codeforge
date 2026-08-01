import { Injectable } from '@angular/core';

declare const window: any;

@Injectable({ providedIn: 'root' })
export class MonacoLoaderService {
  private loadingPromise: Promise<any> | null = null;

  load(): Promise<any> {
    if (!this.loadingPromise) {
      this.loadingPromise = this.doLoad();
    }
    return this.loadingPromise;
  }

  private doLoad(): Promise<any> {
    return new Promise((resolve) => {
      const script = document.createElement('script');
      script.src = 'assets/monaco/min/vs/loader.js';
      script.onload = () => {
        window.require.config({ paths: { vs: 'assets/monaco/min/vs' } });
        window.require(['vs/editor/editor.main'], () => resolve(window.monaco));
      };
      document.body.appendChild(script);
    });
  }
}
