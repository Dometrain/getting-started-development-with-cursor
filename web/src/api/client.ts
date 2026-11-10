import type { components } from './schema';
import { authorizationHeader, setToken } from '../auth/session';

export type DevToken = components['schemas']['DevToken'];
export type DevTokenRequest = components['schemas']['DevTokenRequest'];

export class ApiError extends Error {
  readonly status: number;

  constructor(status: number, message: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

async function readJson<T>(response: Response): Promise<T> {
  if (!response.ok) {
    throw new ApiError(response.status, `Request failed with ${response.status}`);
  }

  return (await response.json()) as T;
}

export async function signIn(username: string): Promise<DevToken> {
  const body: DevTokenRequest = { username };
  const response = await fetch('/auth/dev-token', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  const token = await readJson<DevToken>(response);
  setToken(token.token);
  return token;
}

export async function apiFetch(input: string, init: RequestInit = {}): Promise<Response> {
  const headers = new Headers(init.headers);
  const auth = authorizationHeader();
  for (const [key, value] of Object.entries(auth)) {
    headers.set(key, value);
  }

  return fetch(input, { ...init, headers });
}
