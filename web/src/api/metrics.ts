import type { components } from './schema';
import { ApiError, apiFetch } from './client';

export type BusinessMetrics = components['schemas']['BusinessMetrics'];
export type OptionVoteMetric = components['schemas']['OptionVoteMetric'];

async function readJson<T>(response: Response): Promise<T> {
  if (!response.ok) {
    throw new ApiError(response.status, `Request failed with ${response.status}`);
  }

  return (await response.json()) as T;
}

export async function getMetrics(): Promise<BusinessMetrics> {
  const response = await apiFetch('/metrics');
  return readJson<BusinessMetrics>(response);
}
