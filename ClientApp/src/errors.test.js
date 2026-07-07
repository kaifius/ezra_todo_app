import { describe, it, expect } from 'vitest';
import { extractErrorMessage } from './errors.js';

describe('extractErrorMessage', () => {
  it('flattens an Identity errors map into one string', () => {
    const data = {
      errors: {
        DuplicateUserName: ["Username 'a@b.com' is already taken."],
        DuplicateEmail: ["Email 'a@b.com' is already taken."],
      },
    };
    expect(extractErrorMessage(data, 'fallback')).toBe(
      "Username 'a@b.com' is already taken. Email 'a@b.com' is already taken.",
    );
  });

  it('joins multiple messages under a single error code', () => {
    const data = { errors: { PasswordTooShort: ['Too short.', 'Needs a digit.'] } };
    expect(extractErrorMessage(data, 'fallback')).toBe('Too short. Needs a digit.');
  });

  it('falls back to `detail` when there is no errors map', () => {
    expect(extractErrorMessage({ detail: 'Something specific.' }, 'fallback')).toBe(
      'Something specific.',
    );
  });

  it('falls back to `title` when there is no errors map or detail', () => {
    expect(extractErrorMessage({ title: 'Bad Request' }, 'fallback')).toBe('Bad Request');
  });

  it('prefers the errors map over detail/title', () => {
    const data = { errors: { X: ['From errors.'] }, detail: 'From detail.', title: 'From title.' };
    expect(extractErrorMessage(data, 'fallback')).toBe('From errors.');
  });

  it('uses the fallback for an empty body', () => {
    expect(extractErrorMessage({}, 'fallback')).toBe('fallback');
  });

  it('uses the fallback when the body is null (no/invalid JSON)', () => {
    expect(extractErrorMessage(null, 'fallback')).toBe('fallback');
  });

  it('uses the fallback when the errors map is present but empty', () => {
    expect(extractErrorMessage({ errors: {} }, 'fallback')).toBe('fallback');
  });
});
