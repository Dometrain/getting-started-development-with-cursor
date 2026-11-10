import { useState, type FormEvent, type ReactElement } from 'react';
import { signIn } from '../api/client';

type SignInProps = {
  onSignedIn?: () => void;
};

export function SignIn({ onSignedIn }: SignInProps): ReactElement {
  const [username, setUsername] = useState('');
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function onSubmit(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    setError(null);
    setStatus(null);
    try {
      const token = await signIn(username);
      setStatus(`Signed in as ${token.username}`);
      onSignedIn?.();
    } catch {
      setError('Sign-in failed');
    }
  }

  return (
    <form className="sign-in" onSubmit={onSubmit} aria-label="Sign in">
      <label>
        Username
        <input
          name="username"
          value={username}
          onChange={(event) => setUsername(event.target.value)}
          autoComplete="username"
          required
        />
      </label>
      <button type="submit">Sign in</button>
      {status !== null ? <p>{status}</p> : null}
      {error !== null ? <p role="alert">{error}</p> : null}
    </form>
  );
}
