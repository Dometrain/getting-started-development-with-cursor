import { useState, type FormEvent, type ReactElement } from 'react';
import {
  startSimulation,
  stopSimulation,
  type Simulation,
  type VoteSplit,
} from '../api/simulations';
import { SignIn } from '../auth/SignIn';
import { tokenHasAdminRole } from '../auth/session';
import { MetricsPanel } from '../components/MetricsPanel';

type SplitDraft = {
  optionId: string;
  percent: string;
};

const maximumVoterCount = 2000;
const maximumDurationSeconds = 600;

const defaultSplits: SplitDraft[] = [
  { optionId: '', percent: '70' },
  { optionId: '', percent: '30' },
];

export function AdminPage(): ReactElement {
  const [pollId, setPollId] = useState('');
  const [voterCount, setVoterCount] = useState('1000');
  const [rampUpSeconds, setRampUpSeconds] = useState('30');
  const [durationSeconds, setDurationSeconds] = useState('60');
  const [seed, setSeed] = useState('42');
  const [splits, setSplits] = useState<SplitDraft[]>(defaultSplits);
  const [simulation, setSimulation] = useState<Simulation | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [, setAuthRevision] = useState(0);
  const isAdmin = tokenHasAdminRole();

  function setSplitAt(index: number, patch: Partial<SplitDraft>): void {
    setSplits((current) =>
      current.map((row, i) => (i === index ? { ...row, ...patch } : row)),
    );
  }

  function addSplitRow(): void {
    setSplits((current) => [...current, { optionId: '', percent: '0' }]);
  }

  function removeSplitRow(index: number): void {
    setSplits((current) => (current.length <= 2 ? current : current.filter((_, i) => i !== index)));
  }

  function toVoteSplit(): VoteSplit[] {
    return splits.map((row) => ({
      optionId: row.optionId,
      percent: Number(row.percent),
    }));
  }

  async function onStart(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    setError(null);
    if (Number(voterCount) > maximumVoterCount || Number(durationSeconds) > maximumDurationSeconds) {
      setError('Could not start simulation');
      return;
    }

    try {
      const started = await startSimulation({
        pollId,
        voterCount: Number(voterCount),
        rampUpSeconds: Number(rampUpSeconds),
        durationSeconds: Number(durationSeconds),
        voteSplit: toVoteSplit(),
        seed: Number(seed),
      });
      setSimulation(started);
    } catch {
      setError('Could not start simulation');
    }
  }

  async function onStop(): Promise<void> {
    if (simulation === null) {
      return;
    }
    setError(null);
    try {
      const stopped = await stopSimulation(simulation.id);
      setSimulation(stopped);
    } catch {
      setError('Could not stop simulation');
    }
  }

  return (
    <main className="page card">
      <h1>LivePoll admin</h1>
      <SignIn onSignedIn={() => setAuthRevision((value) => value + 1)} />
      <MetricsPanel />
      {isAdmin ? (
        <section aria-label="Simulator controls">
          <h2>Simulator</h2>
          <form aria-label="Start simulation" onSubmit={onStart}>
            <label>
              Poll id
              <input
                name="pollId"
                value={pollId}
                onChange={(event) => setPollId(event.target.value)}
                required
              />
            </label>
            <label>
              Voter count
              <input
                name="voterCount"
                type="number"
                min={1}
                max={maximumVoterCount}
                value={voterCount}
                onChange={(event) => setVoterCount(event.target.value)}
                required
              />
            </label>
            <label>
              Ramp-up seconds
              <input
                name="rampUpSeconds"
                type="number"
                min={0}
                value={rampUpSeconds}
                onChange={(event) => setRampUpSeconds(event.target.value)}
                required
              />
            </label>
            <label>
              Duration seconds
              <input
                name="durationSeconds"
                type="number"
                min={1}
                max={maximumDurationSeconds}
                value={durationSeconds}
                onChange={(event) => setDurationSeconds(event.target.value)}
                required
              />
            </label>
            <label>
              Seed
              <input
                name="seed"
                type="number"
                value={seed}
                onChange={(event) => setSeed(event.target.value)}
                required
              />
            </label>
            <fieldset aria-label="Vote distribution">
              <legend>Per-option distribution</legend>
              {splits.map((row, index) => (
                <div key={index}>
                  <label>
                    Option id
                    <input
                      name={`optionId-${index}`}
                      value={row.optionId}
                      onChange={(event) => setSplitAt(index, { optionId: event.target.value })}
                      required
                    />
                  </label>
                  <label>
                    Percent
                    <input
                      name={`percent-${index}`}
                      type="number"
                      min={0}
                      max={100}
                      value={row.percent}
                      onChange={(event) => setSplitAt(index, { percent: event.target.value })}
                      required
                    />
                  </label>
                  {splits.length > 2 ? (
                    <button type="button" onClick={() => removeSplitRow(index)}>
                      Remove split
                    </button>
                  ) : null}
                </div>
              ))}
              <button type="button" onClick={addSplitRow}>
                Add split
              </button>
            </fieldset>
            <div className="actions">
              <button type="submit">Start</button>
              <button
                type="button"
                className="danger"
                aria-label="Stop simulation"
                disabled={simulation === null || simulation.status !== 'running'}
                onClick={() => void onStop()}
              >
                Stop
              </button>
            </div>
          </form>
          {simulation !== null ? (
            <p aria-label="Simulation status">
              {simulation.status} ({simulation.id})
            </p>
          ) : null}
        </section>
      ) : (
        <p>Sign in as an admin to control the simulator.</p>
      )}
      {error !== null ? <p role="alert">{error}</p> : null}
    </main>
  );
}
