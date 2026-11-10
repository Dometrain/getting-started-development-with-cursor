import type { components } from './schema';
import { ApiError, apiFetch } from './client';

export type CastVoteRequest = components['schemas']['CastVoteRequest'];
export type CreatePollRequest = components['schemas']['CreatePollRequest'];
export type UpdatePollTitleRequest = components['schemas']['UpdatePollTitleRequest'];
export type PollOptionTextRequest = components['schemas']['PollOptionTextRequest'];
export type Poll = components['schemas']['Poll'];
export type PollOption = components['schemas']['PollOption'];

async function readJson<T>(response: Response): Promise<T> {
  if (!response.ok) {
    throw new ApiError(response.status, `Request failed with ${response.status}`);
  }

  return (await response.json()) as T;
}

export async function createPoll(request: CreatePollRequest): Promise<Poll> {
  const response = await apiFetch('/polls', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });
  return readJson<Poll>(response);
}

export async function castVote(pollId: string, optionId: string): Promise<void> {
  const body: CastVoteRequest = { optionId };
  const response = await apiFetch(`/polls/${pollId}/votes`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!response.ok) {
    throw new ApiError(response.status, `Request failed with ${response.status}`);
  }

  await response.text();
}

export async function getPoll(pollId: string): Promise<Poll> {
  const response = await apiFetch(`/polls/${pollId}`);
  return readJson<Poll>(response);
}

export async function updatePollTitle(
  pollId: string,
  request: UpdatePollTitleRequest,
): Promise<Poll> {
  const response = await apiFetch(`/polls/${pollId}`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });
  return readJson<Poll>(response);
}

export async function addPollOption(
  pollId: string,
  request: PollOptionTextRequest,
): Promise<Poll> {
  const response = await apiFetch(`/polls/${pollId}/options`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });
  return readJson<Poll>(response);
}

export async function updatePollOption(
  pollId: string,
  optionId: string,
  request: PollOptionTextRequest,
): Promise<Poll> {
  const response = await apiFetch(`/polls/${pollId}/options/${optionId}`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });
  return readJson<Poll>(response);
}

export async function removePollOption(pollId: string, optionId: string): Promise<Poll> {
  const response = await apiFetch(`/polls/${pollId}/options/${optionId}`, {
    method: 'DELETE',
  });
  return readJson<Poll>(response);
}
