import { useState, type FormEvent, type ReactElement } from 'react';
import { createPoll, type Poll } from '../api/polls';

const MIN_OPTIONS = 2;
const MAX_OPTIONS = 6;

type CreatePollPageProps = {
  onCreated?: (poll: Poll) => void;
};

export function CreatePollPage({ onCreated }: CreatePollPageProps): ReactElement {
  const [title, setTitle] = useState('');
  const [options, setOptions] = useState<string[]>(['', '']);
  const [error, setError] = useState<string | null>(null);
  const [created, setCreated] = useState<Poll | null>(null);

  function setOptionAt(index: number, value: string): void {
    setOptions((current) => current.map((option, i) => (i === index ? value : option)));
  }

  function addOptionField(): void {
    setOptions((current) => (current.length >= MAX_OPTIONS ? current : [...current, '']));
  }

  function removeOptionField(index: number): void {
    setOptions((current) =>
      current.length <= MIN_OPTIONS ? current : current.filter((_, i) => i !== index),
    );
  }

  async function onSubmit(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    setError(null);
    try {
      const poll = await createPoll({ title, options });
      setCreated(poll);
      onCreated?.(poll);
    } catch {
      setError('Could not create poll');
    }
  }

  return (
    <section className="card" aria-label="Create poll">
      <h2>Create a poll</h2>
      <form onSubmit={onSubmit} aria-label="Create poll form">
        <label>
          Title
          <input
            name="title"
            value={title}
            onChange={(event) => setTitle(event.target.value)}
            required
          />
        </label>
        <ul aria-label="Poll options">
          {options.map((option, index) => (
            <li key={index}>
              <label>
                Option {index + 1}
                <input
                  name={`option-${index}`}
                  value={option}
                  onChange={(event) => setOptionAt(index, event.target.value)}
                  minLength={3}
                  required
                />
              </label>
              {options.length > MIN_OPTIONS ? (
                <button type="button" onClick={() => removeOptionField(index)}>
                  Remove option field
                </button>
              ) : null}
            </li>
          ))}
        </ul>
        {options.length < MAX_OPTIONS ? (
          <button type="button" onClick={addOptionField}>
            Add option field
          </button>
        ) : null}
        <button type="submit">Create poll</button>
      </form>
      {error !== null ? <p role="alert">{error}</p> : null}
      {created !== null ? (
        <p>
          Created poll {created.id}{' '}
          <a href={`/poll/${created.id}`}>Open poll</a>
        </p>
      ) : null}
    </section>
  );
}
