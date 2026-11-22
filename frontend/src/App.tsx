import { useEffect, useState, type FormEvent } from "react";
import { channels } from "./channels";
import { PostCard } from "./PostCard";
import { SearchFilters } from "./SearchFilters";
import type { Filters, Page } from "./types";

const initial: Filters = { channel: "kpszsu", from: "", to: "", q: "" };

export default function App() {
  const [draft, setDraft] = useState(initial);
  const [query, setQuery] = useState({ ...initial, page: 1 });
  const [result, setResult] = useState<Page | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    const params = new URLSearchParams({ page: String(query.page) });
    for (const name of ["from", "to", "q"] as const) {
      if (query[name]) params.set(name, query[name]);
    }
    setLoading(true);
    setError("");
    setResult(null);
    async function search() {
      try {
        const response = await fetch(
          "/channels/" + query.channel + "/posts?" + params,
          { signal: controller.signal },
        );

        if (!response.ok) {
          const problem = await response.json().catch(() => null);
          const details = problem?.errors
            ? Object.values(problem.errors).flat().join(" ")
            : problem?.title;
          throw new Error(
            details || "Search could not be completed. Please try again.",
          );
        }
        const page: Page = await response.json();
        if (!controller.signal.aborted) setResult(page);
      } catch (failure) {
        if (!controller.signal.aborted) {
          setError(
            failure instanceof Error
              ? failure.message
              : "Search could not be completed.",
          );
        }
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    }
    void search();
    
    return () => controller.abort();
  }, [query, attempt]);

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setQuery({ ...draft, page: 1 });
  }

  return (
    <>
      <header className="masthead">
        <a href="/" className="brand">
          Telegram Archive
        </a>
      </header>
      <main>
        <section className="intro">
          <h1>Search archived posts</h1>
          <p>Public posts from three Ukrainian channels.</p>
        </section>
        <div className="workspace">
          <SearchFilters draft={draft} setDraft={setDraft} submit={submit} />
          <section
            aria-label="Search results"
            className="results"
            aria-busy={loading}
          >
            <div className="results-heading">
              <h2>
                {
                  channels.find((channel) => channel.tag === query.channel)
                    ?.name
                }
              </h2>
              <span>Newest first</span>
            </div>
            <div aria-live="polite" role="status">
              {loading && <p className="state">Searching the archive…</p>}
              {error && (
                <div className="state error">
                  <p>{error}</p>
                  <button type="button" onClick={() => setAttempt(attempt + 1)}>
                    Try again
                  </button>
                </div>
              )}
              {result?.items.length === 0 && (
                <div className="state">
                  <h3>No posts found</h3>
                  <p>Try another phrase, channel, or date range.</p>
                </div>
              )}
            </div>
            {result &&
              result.items.map((post) => (
                <PostCard key={post.channelTag + "/" + post.messageId} post={post} query={query.q} />
              ))}
            {result && (
              <nav className="pagination" aria-label="Result pages">
                <button
                  disabled={loading || result.page === 1}
                  onClick={() => setQuery({ ...query, page: query.page - 1 })}
                >
                  ← Previous
                </button>
                <span>Page {result.page}</span>
                <button
                  disabled={loading || !result.hasNextPage}
                  onClick={() => setQuery({ ...query, page: query.page + 1 })}
                >
                  Next →
                </button>
              </nav>
            )}
          </section>
        </div>
      </main>
    </>
  );
}

