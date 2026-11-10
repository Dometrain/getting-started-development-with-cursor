import { useEffect, useState, type ReactElement } from 'react';
import { getMetrics, type BusinessMetrics } from '../api/metrics';

const REFRESH_INTERVAL_MS = 1000;

export function MetricsPanel(): ReactElement {
  const [metrics, setMetrics] = useState<BusinessMetrics | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function refresh(): Promise<void> {
      try {
        const next = await getMetrics();
        if (cancelled) {
          return;
        }
        setMetrics(next);
        setError(null);
      } catch {
        if (!cancelled) {
          setError('Could not load metrics');
        }
      }
    }

    void refresh();
    const timer = window.setInterval(() => {
      void refresh();
    }, REFRESH_INTERVAL_MS);

    return () => {
      cancelled = true;
      window.clearInterval(timer);
    };
  }, []);

  return (
    <aside className="card" aria-label="Metrics panel">
      <h2>Metrics</h2>
      {metrics !== null ? (
        <dl>
          <div>
            <dt>Votes per second</dt>
            <dd aria-label="Votes per second">{metrics.votesPerSecond}</dd>
          </div>
          <div>
            <dt>Total votes</dt>
            <dd aria-label="Total votes">{metrics.totalVotes}</dd>
          </div>
          <div>
            <dt>Active polls</dt>
            <dd aria-label="Active polls">{metrics.activePolls}</dd>
          </div>
          <div>
            <dt>Worker backlog</dt>
            <dd aria-label="Worker backlog">{metrics.votesWaitingForPostgres}</dd>
          </div>
        </dl>
      ) : null}
      {metrics !== null ? (
        <ul aria-label="Votes per option">
          {metrics.votesPerOption.map((option) => (
            <li
              key={`${option.pollId}:${option.optionId}`}
              aria-label={`${option.text} option votes`}
            >
              {option.text}: {option.votes}
            </li>
          ))}
        </ul>
      ) : null}
      {error !== null ? <p role="alert">{error}</p> : null}
    </aside>
  );
}
