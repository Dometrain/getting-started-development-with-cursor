import type { components } from './schema';

export type ResultsSnapshot = components['schemas']['ResultsSnapshot'];

export type ResultsListener = (snapshot: ResultsSnapshot) => void;

/**
 * Hand-written EventSource client for the public results stream.
 * Not generated from OpenAPI — ADR-0006 keeps the stream browser EventSource.
 */
export function openResultsStream(
  pollId: string,
  onSnapshot: ResultsListener,
): EventSource {
  const source = new EventSource(`/polls/${pollId}/results/stream`);
  source.addEventListener('results', (event: MessageEvent<string>) => {
    const snapshot = JSON.parse(event.data) as ResultsSnapshot;
    onSnapshot(snapshot);
  });
  return source;
}
