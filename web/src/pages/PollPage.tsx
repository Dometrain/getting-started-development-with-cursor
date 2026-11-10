import { useEffect, useState, type FormEvent, type ReactElement } from 'react';
import {
  addPollOption,
  castVote,
  getPoll,
  removePollOption,
  updatePollOption,
  updatePollTitle,
  type Poll,
} from '../api/polls';
import { openResultsStream, type ResultsSnapshot } from '../api/resultsStream';
import { tokenHasAdminRole } from '../auth/session';
import { PollChart, type PollChartBar } from '../components/PollChart';

type PollPageProps = {
  pollId: string;
  initialPoll?: Poll;
};

type LiveResults = {
  votesPerSecond: number;
  bars: PollChartBar[];
};

function toLiveResults(snapshot: ResultsSnapshot): LiveResults {
  return {
    votesPerSecond: snapshot.votesPerSecond,
    bars: snapshot.options.map((option) => ({
      option: option.text,
      votes: option.votes,
    })),
  };
}

export function PollPage({ pollId, initialPoll }: PollPageProps): ReactElement {
  const [poll, setPoll] = useState<Poll | null>(initialPoll ?? null);
  const [error, setError] = useState<string | null>(null);
  const [titleDraft, setTitleDraft] = useState(initialPoll?.title ?? '');
  const [newOptionText, setNewOptionText] = useState('');
  const [optionDrafts, setOptionDrafts] = useState<Record<string, string>>(
    Object.fromEntries((initialPoll?.options ?? []).map((option) => [option.id, option.text])),
  );
  const [liveResults, setLiveResults] = useState<LiveResults | null>(null);
  const isAdmin = tokenHasAdminRole();

  useEffect(() => {
    if (initialPoll !== undefined) {
      return;
    }

    let cancelled = false;
    void (async () => {
      try {
        const loaded = await getPoll(pollId);
        if (cancelled) {
          return;
        }
        setPoll(loaded);
        setTitleDraft(loaded.title);
        setOptionDrafts(
          Object.fromEntries(loaded.options.map((option) => [option.id, option.text])),
        );
      } catch {
        if (!cancelled) {
          setError('Could not load poll');
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [pollId, initialPoll]);

  useEffect(() => {
    let cancelled = false;
    let source: EventSource | null = null;

    function connect(): void {
      if (cancelled) {
        return;
      }

      const next = openResultsStream(pollId, (snapshot) => {
        if (!cancelled) {
          setLiveResults(toLiveResults(snapshot));
        }
      });
      source = next;
      next.onerror = () => {
        if (cancelled) {
          return;
        }
        next.onerror = null;
        next.close();
        connect();
      };
    }

    connect();

    return () => {
      cancelled = true;
      source?.close();
    };
  }, [pollId]);

  function applyPoll(next: Poll): void {
    setPoll(next);
    setTitleDraft(next.title);
    setOptionDrafts(Object.fromEntries(next.options.map((option) => [option.id, option.text])));
  }

  async function onSaveTitle(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    setError(null);
    try {
      applyPoll(await updatePollTitle(pollId, { title: titleDraft }));
    } catch {
      setError('Could not update title');
    }
  }

  async function onAddOption(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    setError(null);
    try {
      applyPoll(await addPollOption(pollId, { text: newOptionText }));
      setNewOptionText('');
    } catch {
      setError('Could not add option');
    }
  }

  async function onSaveOption(optionId: string): Promise<void> {
    setError(null);
    try {
      applyPoll(
        await updatePollOption(pollId, optionId, {
          text: optionDrafts[optionId] ?? '',
        }),
      );
    } catch {
      setError('Could not edit option');
    }
  }

  async function onVote(optionId: string): Promise<void> {
    setError(null);
    try {
      await castVote(pollId, optionId);
    } catch {
      setError('Could not vote');
    }
  }

  async function onRemoveOption(optionId: string): Promise<void> {
    setError(null);
    try {
      applyPoll(await removePollOption(pollId, optionId));
    } catch {
      setError('Could not remove option');
    }
  }

  if (poll === null && error === null) {
    return <p>Loading poll…</p>;
  }

  if (poll === null) {
    return <p role="alert">{error}</p>;
  }

  return (
    <section className="card" aria-label="Poll">
      <h2>{poll.title}</h2>
      <ul aria-label="Options">
        {poll.options.map((option) => (
          <li key={option.id}>
            <span>{option.text}</span>
            {poll.ended ? null : (
              <button type="button" onClick={() => void onVote(option.id)}>
                Vote {option.text}
              </button>
            )}
            {isAdmin ? (
              <form
                aria-label={`Edit option ${option.id}`}
                onSubmit={(event) => {
                  event.preventDefault();
                  void onSaveOption(option.id);
                }}
              >
                <label>
                  Edit option
                  <input
                    name={`option-${option.id}`}
                    value={optionDrafts[option.id] ?? option.text}
                    onChange={(event) =>
                      setOptionDrafts((current) => ({
                        ...current,
                        [option.id]: event.target.value,
                      }))
                    }
                    minLength={3}
                    required
                  />
                </label>
                <button type="submit">Save option</button>
                {poll.options.length > 2 ? (
                  <button
                    type="button"
                    aria-label={`Remove option ${option.id}`}
                    onClick={() => void onRemoveOption(option.id)}
                  >
                    Remove option
                  </button>
                ) : null}
              </form>
            ) : null}
          </li>
        ))}
      </ul>

      {isAdmin ? (
        <>
          <form aria-label="Edit poll title" onSubmit={onSaveTitle}>
            <label>
              Title
              <input
                name="title"
                value={titleDraft}
                onChange={(event) => setTitleDraft(event.target.value)}
                required
              />
            </label>
            <button type="submit">Save title</button>
          </form>

          {poll.options.length < 6 ? (
            <form aria-label="Add poll option" onSubmit={onAddOption}>
              <label>
                New option
                <input
                  name="new-option"
                  value={newOptionText}
                  onChange={(event) => setNewOptionText(event.target.value)}
                  minLength={3}
                  required
                />
              </label>
              <button type="submit">Add option</button>
            </form>
          ) : null}
        </>
      ) : null}

      {liveResults !== null ? (
        <section aria-label="Live results">
          <p aria-label="Votes per second">{liveResults.votesPerSecond}</p>
          <ul aria-label="Vote totals">
            {liveResults.bars.map((bar) => (
              <li key={bar.option} aria-label={`${bar.option} votes`}>
                {bar.votes}
              </li>
            ))}
          </ul>
          <PollChart data={liveResults.bars} />
        </section>
      ) : null}

      {error !== null ? <p role="alert">{error}</p> : null}
    </section>
  );
}
