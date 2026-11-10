import '@testing-library/jest-dom/vitest';
import { afterEach } from 'vitest';
import { clearToken } from '../auth/session';

if (typeof globalThis.EventSource === 'undefined') {
  class EventSourceStub {
    static readonly CONNECTING = 0;
    static readonly OPEN = 1;
    static readonly CLOSED = 2;

    readonly url: string;
    readyState = EventSourceStub.OPEN;
    onerror: ((this: EventSource, ev: Event) => unknown) | null = null;

    constructor(url: string | URL) {
      this.url = String(url);
    }

    addEventListener(): void {
      // no-op stub for jsdom
    }

    close(): void {
      this.readyState = EventSourceStub.CLOSED;
    }
  }

  globalThis.EventSource = EventSourceStub as unknown as typeof EventSource;
}

afterEach(() => {
  clearToken();
});
