import { useEffect, useState } from 'react';
import {
  getTasks,
  createTask,
  setTaskCompleted,
  renameTask,
  reorderTasks,
  deleteTask,
} from '../api.js';

// The signed-in user's todo list: create, view, toggle completion, rename,
// reorder, and delete.
export default function TaskList() {
  const [tasks, setTasks] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [title, setTitle] = useState('');
  const [adding, setAdding] = useState(false);
  // Which task is being renamed, plus the in-progress draft text. Only one row is
  // editable at a time.
  const [editingId, setEditingId] = useState(null);
  const [draftTitle, setDraftTitle] = useState('');
  const [savingEdit, setSavingEdit] = useState(false);
  // Disables the move buttons while a reorder request is in flight, avoiding races.
  const [reordering, setReordering] = useState(false);

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

  // Moves the task at `index` one slot up (direction -1) or down (direction +1).
  // Updates the list optimistically, then persists; reverts if the server rejects.
  async function handleMove(index, direction) {
    const target = index + direction;
    if (target < 0 || target >= tasks.length) return;

    const previous = tasks;
    const next = [...tasks];
    [next[index], next[target]] = [next[target], next[index]];

    setError('');
    setTasks(next);
    setReordering(true);
    try {
      await reorderTasks(next.map((t) => t.id));
    } catch (err) {
      setTasks(previous); // roll back to the order the server still has
      setError(err.message);
    } finally {
      setReordering(false);
    }
  }

  function startEdit(task) {
    setError('');
    setEditingId(task.id);
    setDraftTitle(task.title);
  }

  function cancelEdit() {
    setEditingId(null);
    setDraftTitle('');
  }

  async function saveEdit(task) {
    const trimmed = draftTitle.trim();
    if (!trimmed) return; // the server also rejects blank titles; skip the round trip
    if (trimmed === task.title) {
      cancelEdit(); // nothing changed
      return;
    }
    setError('');
    setSavingEdit(true);
    try {
      const updated = await renameTask(task.id, trimmed);
      setTasks((prev) => prev.map((t) => (t.id === updated.id ? updated : t)));
      cancelEdit();
    } catch (err) {
      setError(err.message);
    } finally {
      setSavingEdit(false);
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
          {tasks.map((task, index) => (
            <li key={task.id} className="task">
              <input
                type="checkbox"
                checked={task.isCompleted}
                onChange={() => handleToggle(task)}
                disabled={editingId === task.id}
                aria-label={`Mark "${task.title}" ${task.isCompleted ? 'incomplete' : 'complete'}`}
              />
              {editingId === task.id ? (
                <form
                  className="task-edit"
                  onSubmit={(e) => {
                    e.preventDefault();
                    saveEdit(task);
                  }}
                >
                  <input
                    type="text"
                    aria-label="Edit task title"
                    value={draftTitle}
                    onChange={(e) => setDraftTitle(e.target.value)}
                    onKeyDown={(e) => {
                      if (e.key === 'Escape') cancelEdit();
                    }}
                    autoFocus
                  />
                  <button type="submit" disabled={savingEdit || !draftTitle.trim()}>
                    Save
                  </button>
                  <button type="button" className="task-secondary" onClick={cancelEdit}>
                    Cancel
                  </button>
                </form>
              ) : (
                <>
                  <span className={`task-title${task.isCompleted ? ' completed' : ''}`}>
                    {task.title}
                  </span>
                  <div className="task-move">
                    <button
                      type="button"
                      className="task-secondary"
                      onClick={() => handleMove(index, -1)}
                      disabled={index === 0 || reordering}
                      aria-label={`Move "${task.title}" up`}
                    >
                      ↑
                    </button>
                    <button
                      type="button"
                      className="task-secondary"
                      onClick={() => handleMove(index, 1)}
                      disabled={index === tasks.length - 1 || reordering}
                      aria-label={`Move "${task.title}" down`}
                    >
                      ↓
                    </button>
                  </div>
                  <button
                    type="button"
                    className="task-secondary"
                    onClick={() => startEdit(task)}
                    aria-label={`Rename "${task.title}"`}
                  >
                    Rename
                  </button>
                  <button
                    type="button"
                    className="task-secondary task-delete"
                    onClick={() => handleDelete(task.id)}
                    aria-label={`Delete "${task.title}"`}
                  >
                    Delete
                  </button>
                </>
              )}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
