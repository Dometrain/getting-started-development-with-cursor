import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { createPoll, type Poll } from './api/polls';
import { clearToken, setToken } from './auth/session';
import { PollPage } from './pages/PollPage';

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

function formInput(name: string): HTMLInputElement {
  const form = screen.getByRole('form', { name });
  const input = form.querySelector('input');
  if (input === null) {
    throw new Error(`No input in form ${name}`);
  }
  return input;
}

describe('Frontend poll pages and admin edits', () => {
  beforeEach(() => {
    clearToken();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    clearToken();
  });

  it('CreatePoll_SendsTitleAndTwoToSixOptionsThroughGeneratedClient', async () => {
    const created = samplePoll();
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      expect(String(input)).toBe('/polls');
      expect(init?.method).toBe('POST');
      const body = JSON.parse(String(init?.body)) as {
        title: string;
        options: string[];
      };
      expect(body.title).toBe('Lunch spot');
      expect(body.options).toEqual(['Cafeteria', 'Food truck', 'Park bench']);
      expect(body.options.length).toBeGreaterThanOrEqual(2);
      expect(body.options.length).toBeLessThanOrEqual(6);
      const headers = new Headers(init?.headers);
      expect(headers.get('Authorization')).toBe('Bearer user-token');
      expect(headers.get('Content-Type')).toBe('application/json');
      return new Response(JSON.stringify(created), {
        status: 201,
        headers: { 'Content-Type': 'application/json', Location: `/polls/${created.id}` },
      });
    });
    vi.stubGlobal('fetch', fetchMock);
    setToken('user-token');

    const poll = await createPoll({
      title: 'Lunch spot',
      options: ['Cafeteria', 'Food truck', 'Park bench'],
    });

    expect(poll).toEqual(created);
    expect(fetchMock).toHaveBeenCalledOnce();
  });

  it('PollPage_AdminCanEditTitleAndOptions', async () => {
    const poll = samplePoll();
    const firstOptionId = poll.options[0]?.id;
    const secondOption = poll.options[1];
    if (firstOptionId === undefined || secondOption === undefined) {
      throw new Error('sample poll must have two options');
    }

    setToken(makeJwt({ sub: 'ada', role: 'admin' }));

    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      const method = init?.method ?? 'GET';

      if (method === 'PATCH' && url === `/polls/${poll.id}`) {
        const body = JSON.parse(String(init?.body)) as { title: string };
        return new Response(JSON.stringify({ ...poll, title: body.title }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        });
      }

      if (method === 'POST' && url === `/polls/${poll.id}/options`) {
        const body = JSON.parse(String(init?.body)) as { text: string };
        return new Response(
          JSON.stringify({
            ...poll,
            title: 'Dinner spot',
            options: [
              ...poll.options,
              { id: '9c0d1e2f-3a4b-4c5d-8e6f-7a8b9c0d1e2f', text: body.text },
            ],
          }),
          { status: 200, headers: { 'Content-Type': 'application/json' } },
        );
      }

      if (method === 'PATCH' && url === `/polls/${poll.id}/options/${firstOptionId}`) {
        const body = JSON.parse(String(init?.body)) as { text: string };
        return new Response(
          JSON.stringify({
            ...poll,
            title: 'Dinner spot',
            options: [
              { id: firstOptionId, text: body.text },
              secondOption,
              { id: '9c0d1e2f-3a4b-4c5d-8e6f-7a8b9c0d1e2f', text: 'Bakery' },
            ],
          }),
          { status: 200, headers: { 'Content-Type': 'application/json' } },
        );
      }

      if (
        method === 'DELETE' &&
        url === `/polls/${poll.id}/options/9c0d1e2f-3a4b-4c5d-8e6f-7a8b9c0d1e2f`
      ) {
        return new Response(
          JSON.stringify({
            ...poll,
            title: 'Dinner spot',
            options: [
              { id: firstOptionId, text: 'Cafe' },
              secondOption,
            ],
          }),
          { status: 200, headers: { 'Content-Type': 'application/json' } },
        );
      }

      throw new Error(`Unexpected request ${method} ${url}`);
    });
    vi.stubGlobal('fetch', fetchMock);

    render(<PollPage pollId={poll.id} initialPoll={poll} />);

    expect(screen.getByRole('form', { name: 'Edit poll title' })).toBeInTheDocument();
    expect(screen.getByRole('form', { name: 'Add poll option' })).toBeInTheDocument();
    expect(
      screen.getByRole('form', { name: `Edit option ${firstOptionId}` }),
    ).toBeInTheDocument();

    fireEvent.change(formInput('Edit poll title'), { target: { value: 'Dinner spot' } });
    fireEvent.submit(screen.getByRole('form', { name: 'Edit poll title' }));
    await waitFor(() => {
      expect(screen.getByRole('heading', { name: 'Dinner spot' })).toBeInTheDocument();
    });

    fireEvent.change(formInput('Add poll option'), { target: { value: 'Bakery' } });
    fireEvent.submit(screen.getByRole('form', { name: 'Add poll option' }));
    await waitFor(() => {
      expect(screen.getByText('Bakery')).toBeInTheDocument();
    });

    fireEvent.change(formInput(`Edit option ${firstOptionId}`), {
      target: { value: 'Cafe' },
    });
    fireEvent.submit(screen.getByRole('form', { name: `Edit option ${firstOptionId}` }));
    await waitFor(() => {
      expect(screen.getByText('Cafe')).toBeInTheDocument();
    });

    fireEvent.click(
      screen.getByRole('button', {
        name: 'Remove option 9c0d1e2f-3a4b-4c5d-8e6f-7a8b9c0d1e2f',
      }),
    );
    await waitFor(() => {
      expect(screen.queryByText('Bakery')).not.toBeInTheDocument();
    });

    expect(fetchMock).toHaveBeenCalled();
  });

  it('PollPage_NonAdminDoesNotSeeEditControls', () => {
    const poll = samplePoll();
    setToken(makeJwt({ sub: 'bob' }));

    render(<PollPage pollId={poll.id} initialPoll={poll} />);

    expect(screen.getByRole('heading', { name: 'Lunch spot' })).toBeInTheDocument();
    expect(screen.getByText('Cafeteria')).toBeInTheDocument();
    expect(screen.queryByRole('form', { name: 'Edit poll title' })).not.toBeInTheDocument();
    expect(screen.queryByRole('form', { name: 'Add poll option' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Save option' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Remove option/ })).not.toBeInTheDocument();
  });
});
