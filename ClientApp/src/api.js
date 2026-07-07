// Thin wrapper around the ASP.NET Core Identity endpoints exposed under /account.
// `credentials: 'include'` is essential: it tells the browser to send/receive the
// HttpOnly identity cookie that backs our session.
import { extractErrorMessage } from './errors.js';

function request(method, path, body) {
  return fetch(path, {
    method,
    headers: body ? { 'Content-Type': 'application/json' } : undefined,
    credentials: 'include',
    body: body ? JSON.stringify(body) : undefined,
  });
}

function post(path, body) {
  return request('POST', path, body);
}

// Read the response body (if any) and reduce it to a display string.
async function readError(res, fallback) {
  let data = null;
  try {
    data = await res.json();
  } catch {
    // no/invalid JSON body — extractErrorMessage falls back
  }
  return extractErrorMessage(data, fallback);
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

// --- Tasks (todo CRUD under /api/tasks, scoped to the session's user) ---

// Each task is { id, title, isCompleted, createdAt }.
export async function getTasks() {
  const res = await fetch('/api/tasks', { credentials: 'include' });
  if (!res.ok) throw new Error(await readError(res, 'Could not load tasks.'));
  return res.json();
}

export async function createTask(title) {
  const res = await post('/api/tasks', { title });
  if (!res.ok) throw new Error(await readError(res, 'Could not add task.'));
  return res.json();
}

// Toggles completion. The API's PUT requires a title, so resend the task's
// existing one alongside the new completed state.
export async function setTaskCompleted(task, isCompleted) {
  const res = await request('PUT', `/api/tasks/${task.id}`, { title: task.title, isCompleted });
  if (!res.ok) throw new Error(await readError(res, 'Could not update task.'));
  return res.json();
}

// Renames a task. Omitting isCompleted leaves it unchanged server-side.
export async function renameTask(id, title) {
  const res = await request('PUT', `/api/tasks/${id}`, { title });
  if (!res.ok) throw new Error(await readError(res, 'Could not rename task.'));
  return res.json();
}

// Persists a new ordering. `ids` is the user's full task-id list in display order.
export async function reorderTasks(ids) {
  const res = await request('PUT', '/api/tasks/order', { ids });
  if (!res.ok) throw new Error(await readError(res, 'Could not reorder tasks.'));
}

export async function deleteTask(id) {
  const res = await request('DELETE', `/api/tasks/${id}`);
  if (!res.ok) throw new Error(await readError(res, 'Could not delete task.'));
}
