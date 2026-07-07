import { useEffect, useState } from 'react';
import { getTasks, createTask, setTaskCompleted, deleteTask } from '../api.js';

// The signed-in user's todo list: create, view, toggle completion, and delete.
// (Renaming and reordering aren't implemented yet.)
export default function TaskList() {
  const [tasks, setTasks] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [title, setTitle] = useState('');
  const [adding, setAdding] = useState(false);

  // Load the list once on mount.
  useEffect(() => {
    getTasks()
      .then(setTasks)
      .catch((err) => setError(err.message))
      .finally(() => setLoading(false));
  }, []);

  async function handleAdd(e) {
    e.preventDefault();
    const trimmed = title.trim();
    if (!trimmed) return; // the server also rejects this; skip the round trip
    setError('');
    setAdding(true);
    try {
      const created = await createTask(trimmed);
      setTasks((prev) => [...prev, created]);
      setTitle('');
    } catch (err) {
      setError(err.message);
    } finally {
      setAdding(false);
    }
  }

  async function handleToggle(task) {
    setError('');
    try {
      const updated = await setTaskCompleted(task, !task.isCompleted);
      setTasks((prev) => prev.map((t) => (t.id === updated.id ? updated : t)));
    } catch (err) {
      setError(err.message);
    }
  }

  async function handleDelete(id) {
    setError('');
    try {
      await deleteTask(id);
      setTasks((prev) => prev.filter((t) => t.id !== id));
    } catch (err) {
      setError(err.message);
    }
  }

  return (
    <section className="tasks-section">
      <form className="add-task" onSubmit={handleAdd}>
        <input
          type="text"
          aria-label="New task"
          placeholder="Add a task…"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
        />
        <button type="submit" disabled={adding || !title.trim()}>
          Add
        </button>
      </form>

      {error && <p className="error" role="alert">{error}</p>}

      {loading ? (
        <p className="muted">Loading tasks…</p>
      ) : tasks.length === 0 ? (
        <p className="muted">No tasks yet. Add your first one above.</p>
      ) : (
        <ul className="tasks">
          {tasks.map((task) => (
            <li key={task.id} className="task">
              <input
                type="checkbox"
                checked={task.isCompleted}
                onChange={() => handleToggle(task)}
                aria-label={`Mark "${task.title}" ${task.isCompleted ? 'incomplete' : 'complete'}`}
              />
              <span className={`task-title${task.isCompleted ? ' completed' : ''}`}>
                {task.title}
              </span>
              <button
                type="button"
                className="task-delete"
                onClick={() => handleDelete(task.id)}
                aria-label={`Delete "${task.title}"`}
              >
                Delete
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
