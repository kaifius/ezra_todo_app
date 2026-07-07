import { useState } from 'react';
import { login, register } from '../api.js';

// Shared login/register form. `mode` is 'login' or 'register'; on success it calls
// onAuthenticated so the parent can refresh the session.
export default function AuthForm({ mode, onAuthenticated, onSwitchMode }) {
  const isRegister = mode === 'register';
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e) {
    e.preventDefault();
    setError('');
    setSubmitting(true);
    try {
      if (isRegister) {
        // Register, then log in so the user lands in an authenticated session.
        await register(email, password);
      }
      await login(email, password);
      onAuthenticated();
    } catch (err) {
      setError(err.message);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form className="card" onSubmit={handleSubmit}>
      <h1>{isRegister ? 'Create account' : 'Sign in'}</h1>

      <label htmlFor="email">Email</label>
      <input
        id="email"
        type="email"
        autoComplete="email"
        value={email}
        onChange={(e) => setEmail(e.target.value)}
        required
      />

      <label htmlFor="password">Password</label>
      <input
        id="password"
        type="password"
        autoComplete={isRegister ? 'new-password' : 'current-password'}
        value={password}
        onChange={(e) => setPassword(e.target.value)}
        required
      />

      {error && <p className="error" role="alert">{error}</p>}

      <button type="submit" disabled={submitting}>
        {submitting ? 'Please wait…' : isRegister ? 'Create account' : 'Sign in'}
      </button>

      <p className="switch">
        {isRegister ? 'Already have an account?' : "Don't have an account?"}{' '}
        <button type="button" className="link" onClick={onSwitchMode}>
          {isRegister ? 'Sign in' : 'Create one'}
        </button>
      </p>
    </form>
  );
}
