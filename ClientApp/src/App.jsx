import { useEffect, useState } from 'react';
import AuthForm from './components/AuthForm.jsx';
import { getCurrentUser, logout } from './api.js';

export default function App() {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);
  const [mode, setMode] = useState('login'); // 'login' | 'register'

  // On load, ask the server whether we already have a valid session cookie.
  async function refreshSession() {
    setUser(await getCurrentUser());
  }

  useEffect(() => {
    refreshSession().finally(() => setLoading(false));
  }, []);

  async function handleLogout() {
    await logout();
    setUser(null);
    setMode('login');
  }

  if (loading) {
    return <main className="container"><p>Loading…</p></main>;
  }

  if (user) {
    return (
      <main className="container">
        <div className="card">
          <h1>Task Manager</h1>
          <p>Signed in as <strong>{user.email}</strong>.</p>
          <p className="muted">Auth flow complete — task features coming next.</p>
          <button onClick={handleLogout}>Sign out</button>
        </div>
      </main>
    );
  }

  return (
    <main className="container">
      <AuthForm
        mode={mode}
        onAuthenticated={refreshSession}
        onSwitchMode={() => setMode(mode === 'login' ? 'register' : 'login')}
      />
    </main>
  );
}
