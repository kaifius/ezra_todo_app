// Thin wrapper around the ASP.NET Core Identity endpoints exposed under /account.
// `credentials: 'include'` is essential: it tells the browser to send/receive the
// HttpOnly identity cookie that backs our session.

async function post(path, body) {
  const res = await fetch(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: body ? JSON.stringify(body) : undefined,
  });
  return res;
}

// Identity returns validation failures as an RFC 7807 problem-details object with
// an `errors` map ({ code: [messages] }). Flatten it into one readable string.
async function readError(res, fallback) {
  try {
    const data = await res.json();
    if (data?.errors) {
      const messages = Object.values(data.errors).flat();
      if (messages.length) return messages.join(' ');
    }
    if (data?.detail) return data.detail;
    if (data?.title) return data.title;
  } catch {
    // no/invalid JSON body — fall through
  }
  return fallback;
}

export async function register(email, password) {
  const res = await post('/account/register', { email, password });
  if (!res.ok) throw new Error(await readError(res, 'Registration failed.'));
}

export async function login(email, password) {
  // On success the server responds with a Set-Cookie session (HttpOnly).
  const res = await post('/account/login', { email, password });
  if (!res.ok) {
    throw new Error(
      res.status === 401
        ? 'Incorrect email or password.'
        : await readError(res, 'Login failed.'),
    );
  }
}

export async function logout() {
  await post('/account/logout');
}

// Returns the current user ({ email }) or null if not signed in.
export async function getCurrentUser() {
  const res = await fetch('/account/me', { credentials: 'include' });
  if (res.status === 401) return null;
  if (!res.ok) throw new Error('Could not load session.');
  return res.json();
}
