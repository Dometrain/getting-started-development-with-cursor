import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import type { ResultsSnapshot } from './api/resultsStream';
import { clearToken, setToken } from './auth/session';
import type { Poll } from './api/polls';
import { PollPage } from './pages/PollPage';

const webRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

function encodeBase64Url(value: string): string {
  return btoa(value).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/u, '');
}

function makeJwt(claims: Record<string, unknown>): string {
  const header = encodeBase64Url(JSON.stringify({ alg: 'none', typ: 'JWT' }));
  const payload = encodeBase64Url(JSON.stringify(claims));
  return `${header}.${payload}.sig`;
}

function samplePoll(overrides: Partial<Poll> = {}): Poll {
  return {
    id: '3f1c0a2e-7b4d-4e1a-9c3f-6a8b2d4e5f70',
    title: 'Lunch spot',
    options: [
      { id: '8a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d', text: 'Cafeteria' },
      { id: '1b2c3d4e-5f6a-4b7c-8d9e-0f1a2b3c4d5e', text: 'Food truck' },
    ],
    durationSeconds: 600,
    createdAt: '2026-11-06T17:00:00Z',
    endsAt: '2026-11-06T17:10:00Z',
    ended: false,
    ...overrides,
  };
}

type ResultsListener = (event: MessageEvent<string>) => void;

class FakeEventSource {
  static readonly CONNECTING = 0;
  static readonly OPEN = 1;
  static readonly CLOSED = 2;
  static instances: FakeEventSource[] = [];

  readonly url: string;
  readonly init: EventSourceInit | undefined;
  readyState = FakeEventSource.CONNECTING;
  onerror: ((this: EventSource, ev: Event) => unknown) | null = null;
  private readonly listeners = new Map<string, ResultsListener>();

  constructor(url: string | URL, init?: EventSourceInit) {
    this.url = String(url);
    this.init = init;
    FakeEventSource.instances.push(this);
    this.readyState = FakeEventSource.OPEN;
  }

  addEventListener(type: string, listener: EventListenerOrEventListenerObject): void {
    if (typeof listener === 'function') {
      this.listeners.set(type, listener as ResultsListener);
    }
  }

  close(): void {
    this.readyState = FakeEventSource.CLOSED;
  }

  emitResults(snapshot: ResultsSnapshot): void {
    const listener = this.listeners.get('results');
    listener?.(
      new MessageEvent('results', {
        data: JSON.stringify(snapshot),
      }),
    );
  }

  end(): void {
    this.readyState = FakeEventSource.CLOSED;
    const handler = this.onerror;
    if (handler !== null) {
      handler.call(this as unknown as EventSource, new Event('error'));
    }
  }
}

describe('Frontend vote page and live chart', () => {
  beforeEach(() => {
    clearToken();
    FakeEventSource.instances = [];
    vi.stubGlobal('EventSource', FakeEventSource);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    clearToken();
  });

  it('PollPage_ChartReplacesTotalsFromSseEvent', async () => {
    const poll = samplePoll();
    setToken(makeJwt({ sub: 'bob' }));

    render(<PollPage pollId={poll.id} initialPoll={poll} />);

    await waitFor(() => {
      expect(FakeEventSource.instances).toHaveLength(1);
    });

    const source = FakeEventSource.instances[0];
    expect(source).toBeDefined();
    expect(source?.url).toBe(`/polls/${poll.id}/results/stream`);
    expect(source?.init).toBeUndefined();

    source?.emitResults({
      pollId: poll.id,
      ended: false,
      votesPerSecond: 4,
      options: [
        { optionId: poll.options[0]!.id, text: 'Cafeteria', votes: 10 },
        { optionId: poll.options[1]!.id, text: 'Food truck', votes: 3 },
      ],
    });

    await waitFor(() => {
      expect(screen.getByLabelText('Votes per second')).toHaveTextContent('4');
      expect(screen.getByLabelText('Cafeteria votes')).toHaveTextContent('10');
      expect(screen.getByLabelText('Food truck votes')).toHaveTextContent('3');
    });

    source?.emitResults({
      pollId: poll.id,
      ended: false,
      votesPerSecond: 1,
      options: [
        { optionId: poll.options[0]!.id, text: 'Cafeteria', votes: 12 },
        { optionId: poll.options[1]!.id, text: 'Food truck', votes: 9 },
      ],
    });

    await waitFor(() => {
      expect(screen.getByLabelText('Votes per second')).toHaveTextContent('1');
      expect(screen.getByLabelText('Cafeteria votes')).toHaveTextContent('12');
      expect(screen.getByLabelText('Food truck votes')).toHaveTextContent('9');
    });

    expect(screen.queryByLabelText('Votes per second')).not.toHaveTextContent('4');
    expect(screen.queryByLabelText('Cafeteria votes')).not.toHaveTextContent('10');
    expect(screen.queryByLabelText('Food truck votes')).not.toHaveTextContent('3');
    expect(screen.queryAllByLabelText('Votes per second')).toHaveLength(1);

    const chartSource = readFileSync(path.join(webRoot, 'src/components/PollChart.tsx'), 'utf8');
    expect(chartSource).toContain('isAnimationActive={false}');
  });

  it('PollPage_ReopensResultsStreamWhenItEnds', async () => {
    const poll = samplePoll();
    setToken(makeJwt({ sub: 'bob' }));

    render(<PollPage pollId={poll.id} initialPoll={poll} />);

    await waitFor(() => {
      expect(FakeEventSource.instances).toHaveLength(1);
    });

    const first = FakeEventSource.instances[0];
    expect(first?.url).toBe(`/polls/${poll.id}/results/stream`);
    expect(first?.init).toBeUndefined();

    first?.end();

    await waitFor(() => {
      expect(FakeEventSource.instances).toHaveLength(2);
    });

    const second = FakeEventSource.instances[1];
    expect(second?.url).toBe(`/polls/${poll.id}/results/stream`);
    expect(second?.init).toBeUndefined();
    expect(second).not.toBe(first);
  });
});
