// Turn a parsed ASP.NET Core Identity error body into a single display string.
// Identity returns validation failures as an RFC 7807 problem-details object with
// an `errors` map ({ code: [messages] }); other failures may carry `detail` or
// `title`. `data` may be null (e.g. no/invalid JSON body) — then we use fallback.
export function extractErrorMessage(data, fallback) {
  if (data?.errors) {
    const messages = Object.values(data.errors).flat();
    if (messages.length) return messages.join(' ');
  }
  if (data?.detail) return data.detail;
  if (data?.title) return data.title;
  return fallback;
}
