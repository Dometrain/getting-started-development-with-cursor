import type { components } from './schema';
import { ApiError, apiFetch } from './client';

export type StartSimulationRequest = components['schemas']['StartSimulationRequest'];
export type Simulation = components['schemas']['Simulation'];
export type VoteSplit = components['schemas']['VoteSplit'];

async function readJson<T>(response: Response): Promise<T> {
  if (!response.ok) {
    throw new ApiError(response.status, `Request failed with ${response.status}`);
  }

  return (await response.json()) as T;
}

export async function startSimulation(request: StartSimulationRequest): Promise<Simulation> {
  const response = await apiFetch('/admin/simulations', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });
  return readJson<Simulation>(response);
}

export async function getSimulation(simulationId: string): Promise<Simulation> {
  const response = await apiFetch(`/admin/simulations/${simulationId}`);
  return readJson<Simulation>(response);
}

export async function stopSimulation(simulationId: string): Promise<Simulation> {
  const response = await apiFetch(`/admin/simulations/${simulationId}`, {
    method: 'DELETE',
  });
  return readJson<Simulation>(response);
}
