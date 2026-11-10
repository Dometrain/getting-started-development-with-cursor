import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import type { BusinessMetrics } from './api/metrics';
import { clearToken, setToken } from './auth/session';
import { MetricsPanel } from './components/MetricsPanel';
import { AdminPage } from './pages/AdminPage';

function encodeBase64Url(value: string): string {
  return btoa(value).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/u, '');
}

function makeJwt(claims: Record<string, unknown>): string {
  const header = encodeBase64Url(JSON.stringify({ alg: 'none', typ: 'JWT' }));
  const payload = encodeBase64Url(JSON.stringify(claims));
  return `${header}.${payload}.sig`;
}

function sampleMetrics(overrides: Partial<BusinessMetrics> = {}): BusinessMetrics {
  return {
    votesPerSecond: 3,
    totalVotes: 6,
    activePolls: 1,
    votesPerOption: [
      {
        pollId: '3f1c0a2e-7b4d-4e1a-9c3f-6a8b2d4e5f70',
        optionId: '8a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d',
        text: 'Cafeteria',
        votes: 4,
      },
      {
        pollId: '3f1c0a2e-7b4d-4e1a-9c3f-6a8b2d4e5f70',
        optionId: '1b2c3d4e-5f6a-4b7c-8d9e-0f1a2b3c4d5e',
        text: 'Food truck',
        votes: 2,
      },
    ],
    votesWaitingForPostgres: 1,
    ...overrides,
  };
}

describe('MetricsPanelTests', () => {
  beforeEach(() => {
    clearToken();
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
    clearToken();
  });

  it('ShowsFiveMetricsAndRefreshesEverySecond', async () => {
    setToken(makeJwt({ sub: 'ada', role: 'admin' }));
    let callCount = 0;
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      expect(String(input)).toBe('/metrics');
      callCount += 1;
      const payload =
        callCount === 1
          ? sampleMetrics()
          : sampleMetrics({
              votesPerSecond: 5,
              totalVotes: 9,
              activePolls: 2,
              votesWaitingForPostgres: 0,
              votesPerOption: [
                {
                  pollId: '3f1c0a2e-7b4d-4e1a-9c3f-6a8b2d4e5f70',
                  optionId: '8a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d',
                  text: 'Cafeteria',
                  votes: 7,
                },
              ],
            });
      return new Response(JSON.stringify(payload), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      });
    });
    vi.stubGlobal('fetch', fetchMock);

    render(<MetricsPanel />);

    await waitFor(() => {
      expect(screen.getByLabelText('Votes per second')).toHaveTextContent('3');
    });
    expect(screen.getByLabelText('Total votes')).toHaveTextContent('6');
    expect(screen.getByLabelText('Active polls')).toHaveTextContent('1');
    expect(screen.getByLabelText('Worker backlog')).toHaveTextContent('1');
    expect(screen.getByLabelText('Votes per option')).toBeInTheDocument();
    expect(screen.getByLabelText('Cafeteria option votes')).toHaveTextContent('4');
    expect(screen.getByLabelText('Food truck option votes')).toHaveTextContent('2');
    expect(fetchMock).toHaveBeenCalledOnce();

    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000);
    });

    await waitFor(() => {
      expect(fetchMock.mock.calls.length).toBeGreaterThanOrEqual(2);
      expect(screen.getByLabelText('Votes per second')).toHaveTextContent('5');
    });
    expect(screen.getByLabelText('Total votes')).toHaveTextContent('9');
    expect(screen.getByLabelText('Active polls')).toHaveTextContent('2');
    expect(screen.getByLabelText('Worker backlog')).toHaveTextContent('0');
  });
});

describe('Admin simulator controls', () => {
  beforeEach(() => {
    clearToken();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    clearToken();
  });

  it('AdminPage_StartsAndStopsSimulationWithBearerToken', async () => {
    const pollId = '3f1c0a2e-7b4d-4e1a-9c3f-6a8b2d4e5f70';
    const optionA = '8a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d';
    const optionB = '1b2c3d4e-5f6a-4b7c-8d9e-0f1a2b3c4d5e';
    const simulationId = '9c0d1e2f-3a4b-4c5d-8e6f-7a8b9c0d1e2f';
    const adminToken = makeJwt({ sub: 'ada', role: 'admin' });
    setToken(adminToken);

    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      const method = init?.method ?? 'GET';
      const headers = new Headers(init?.headers);

      if (url === '/metrics') {
        return new Response(JSON.stringify(sampleMetrics()), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        });
      }

      if (method === 'POST' && url === '/admin/simulations') {
        expect(headers.get('Authorization')).toBe(`Bearer ${adminToken}`);
        expect(headers.get('Content-Type')).toBe('application/json');
        const body = JSON.parse(String(init?.body)) as {
          pollId: string;
          voterCount: number;
          rampUpSeconds: number;
          durationSeconds: number;
          seed: number;
          voteSplit: { optionId: string; percent: number }[];
        };
        expect(body).toEqual({
          pollId,
          voterCount: 1000,
          rampUpSeconds: 30,
          durationSeconds: 60,
          seed: 42,
          voteSplit: [
            { optionId: optionA, percent: 70 },
            { optionId: optionB, percent: 30 },
          ],
        });
        return new Response(
          JSON.stringify({
            id: simulationId,
            status: 'running',
            ...body,
            startedAt: '2026-11-06T17:01:00Z',
          }),
          {
            status: 201,
            headers: {
              'Content-Type': 'application/json',
              Location: `/admin/simulations/${simulationId}`,
            },
          },
        );
      }

      if (method === 'DELETE' && url === `/admin/simulations/${simulationId}`) {
        expect(headers.get('Authorization')).toBe(`Bearer ${adminToken}`);
        return new Response(
          JSON.stringify({
            id: simulationId,
            status: 'stopped',
            pollId,
            voterCount: 1000,
            rampUpSeconds: 30,
            durationSeconds: 60,
            voteSplit: [
              { optionId: optionA, percent: 70 },
              { optionId: optionB, percent: 30 },
            ],
            seed: 42,
            startedAt: '2026-11-06T17:01:00Z',
            stoppedAt: '2026-11-06T17:02:00Z',
          }),
          { status: 200, headers: { 'Content-Type': 'application/json' } },
        );
      }

      throw new Error(`Unexpected request ${method} ${url}`);
    });
    vi.stubGlobal('fetch', fetchMock);

    render(<AdminPage />);

    fireEvent.change(screen.getByLabelText('Poll id'), { target: { value: pollId } });
    fireEvent.change(screen.getByLabelText('Voter count'), { target: { value: '1000' } });
    fireEvent.change(screen.getByLabelText('Ramp-up seconds'), { target: { value: '30' } });
    fireEvent.change(screen.getByLabelText('Duration seconds'), { target: { value: '60' } });
    fireEvent.change(screen.getByLabelText('Seed'), { target: { value: '42' } });

    const optionIdInputs = screen.getAllByLabelText('Option id');
    const percentInputs = screen.getAllByLabelText('Percent');
    fireEvent.change(optionIdInputs[0]!, { target: { value: optionA } });
    fireEvent.change(percentInputs[0]!, { target: { value: '70' } });
    fireEvent.change(optionIdInputs[1]!, { target: { value: optionB } });
    fireEvent.change(percentInputs[1]!, { target: { value: '30' } });

    fireEvent.submit(screen.getByRole('form', { name: 'Start simulation' }));

    await waitFor(() => {
      expect(screen.getByLabelText('Simulation status')).toHaveTextContent('running');
    });

    fireEvent.click(screen.getByRole('button', { name: 'Stop simulation' }));

    await waitFor(() => {
      expect(screen.getByLabelText('Simulation status')).toHaveTextContent('stopped');
    });
  });

  it('AdminPage_BlocksVoterCountAbove2000', async () => {
    const fetchMock = await renderAdmin();
    expect(screen.getByLabelText('Voter count')).toHaveAttribute('max', '2000');

    fireEvent.change(screen.getByLabelText('Voter count'), { target: { value: '2001' } });
    fireEvent.submit(screen.getByRole('form', { name: 'Start simulation' }));

    expect(simulationPosts(fetchMock)).toEqual([]);
    expect(await screen.findByRole('alert')).toHaveTextContent('Could not start simulation');
  });

  it('AdminPage_BlocksDurationAbove600Seconds', async () => {
    const fetchMock = await renderAdmin();
    expect(screen.getByLabelText('Duration seconds')).toHaveAttribute('max', '600');

    fireEvent.change(screen.getByLabelText('Duration seconds'), { target: { value: '601' } });
    fireEvent.submit(screen.getByRole('form', { name: 'Start simulation' }));

    expect(simulationPosts(fetchMock)).toEqual([]);
    expect(await screen.findByRole('alert')).toHaveTextContent('Could not start simulation');
  });

  async function renderAdmin(): Promise<ReturnType<typeof vi.fn>> {
    setToken(makeJwt({ sub: 'ada', role: 'admin' }));
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      if (String(input) === '/metrics') {
        return new Response(JSON.stringify(sampleMetrics()), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        });
      }

      throw new Error(`Unexpected request ${String(input)}`);
    });
    vi.stubGlobal('fetch', fetchMock);
    render(<AdminPage />);
    await waitFor(() => {
      expect(screen.getByLabelText('Voter count')).toBeInTheDocument();
    });
    return fetchMock;
  }

  function simulationPosts(fetchMock: ReturnType<typeof vi.fn>): unknown[][] {
    return fetchMock.mock.calls.filter((call) => String(call[0]) === '/admin/simulations');
  }
});
