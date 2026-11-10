import { useState, type ReactElement } from 'react';
import { SignIn } from './auth/SignIn';
import { CreatePollPage } from './pages/CreatePollPage';
import { PollPage } from './pages/PollPage';
import { AdminPage } from './pages/AdminPage';
import { MetricsPanel } from './components/MetricsPanel';
import type { Poll } from './api/polls';

const pollPagePattern =
  /^\/poll\/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$/i;

export function pollIdFromPath(pathname: string): string | null {
  return pollPagePattern.exec(pathname)?.[1] ?? null;
}

function isAdminPath(pathname: string): boolean {
  return pathname === '/admin' || pathname.startsWith('/admin/');
}

export function App(): ReactElement {
  const [poll, setPoll] = useState<Poll | null>(null);
  const sharedPollId = pollIdFromPath(window.location.pathname);

  if (isAdminPath(window.location.pathname)) {
    return <AdminPage />;
  }

  if (sharedPollId !== null) {
    return (
      <main className="page">
        <h1>LivePoll</h1>
        <SignIn />
        <PollPage pollId={sharedPollId} />
      </main>
    );
  }

  return (
    <main className="page">
      <h1>LivePoll</h1>
      <SignIn />
      <MetricsPanel />
      <CreatePollPage onCreated={setPoll} />
      {poll !== null ? <PollPage pollId={poll.id} initialPoll={poll} /> : null}
    </main>
  );
}
