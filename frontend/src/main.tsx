import React, { useEffect, useState, type FormEvent } from "react";
import { createRoot } from "react-dom/client";
import "./style.css";

type Post = {
  channelTag: string;
  messageId: number;
  postedAt: string;
  text: string | null;
  views: number | null;
  mediaUrls: string[];
  forwardFrom: string | null;
  isEdited: boolean | null;
  url: string;
};
type Page = {
  page: number;
  pageSize: number;
  hasNextPage: boolean;
  items: Post[];
};
type Filters = { channel: string; from: string; to: string; q: string };
const channels = [
  { tag: "kpszsu", name: "Повітряні Сили ЗС України" },
  { tag: "dsns_telegram", name: "ДСНС України" },
  { tag: "ukrainenow", name: "Ukraine NOW" },
];
const initial: Filters = { channel: "kpszsu", from: "", to: "", q: "" };
const dateFormat = new Intl.DateTimeFormat("en-GB", {
  dateStyle: "medium",
  timeStyle: "short",
  timeZone: "UTC",
});

function App() {
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
        console.log({ query });
        const response = await fetch(
          "/channels/" + query.channel + "/posts?" + params,
          { signal: controller.signal },
        );
        console.log({ response });

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
    
    return () => {
    console.log("Effect cleanup — aborting:", query);
    controller.abort();
  };
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
          <aside>
            <form onSubmit={submit}>
              <h2>Filters</h2>
              <label htmlFor="channel">Channel</label>
              <select
                id="channel"
                value={draft.channel}
                onChange={(event) =>
                  setDraft({ ...draft, channel: event.target.value })
                }
              >
                {channels.map((channel) => (
                  <option key={channel.tag} value={channel.tag}>
                    {channel.name}
                  </option>
                ))}
              </select>
              <label htmlFor="query">Text</label>
              <input
                id="query"
                type="search"
                placeholder="Words or a phrase"
                value={draft.q}
                onChange={(event) =>
                  setDraft({ ...draft, q: event.target.value })
                }
              />
              <fieldset>
                <legend>
                  Publication dates <span>(UTC)</span>
                </legend>
                <div className="dates">
                  <div>
                    <label htmlFor="from">From</label>
                    <input
                      id="from"
                      type="date"
                      value={draft.from}
                      onChange={(event) =>
                        setDraft({ ...draft, from: event.target.value })
                      }
                    />
                  </div>
                  <div>
                    <label htmlFor="to">To</label>
                    <input
                      id="to"
                      type="date"
                      min={draft.from || undefined}
                      value={draft.to}
                      onChange={(event) =>
                        setDraft({ ...draft, to: event.target.value })
                      }
                    />
                  </div>
                </div>
              </fieldset>
              <button className="primary" type="submit">
                Search posts <span aria-hidden="true">→</span>
              </button>
            </form>
            <p className="availability">
              Source availability is unknown. Archived posts may have changed
              since collection.
            </p>
          </aside>
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
                <article
                  className="post"
                  key={post.channelTag + "/" + post.messageId}
                >
                  <div className="post-meta">
                    <time dateTime={post.postedAt}>
                      {dateFormat.format(new Date(post.postedAt))} UTC
                    </time>
                    <span>
                      {post.views !== null &&
                        post.views.toLocaleString() + " views"}
                    </span>
                  </div>
                  <p className="post-text" lang="uk">
                    {post.text || "Media post"}
                  </p>
                  {post.forwardFrom && (
                    <p className="forward">Forwarded from {post.forwardFrom}</p>
                  )}
                  <div className="post-bottom">
                    <div className="links">
                      <a
                        href={post.url}
                        target="_blank"
                        rel="noopener noreferrer"
                      >
                        Original post
                      </a>
                      {post.mediaUrls
                        .filter((url) => /^https?:\/\//i.test(url))
                        .map((url, index) => (
                          <a
                            key={url}
                            href={url}
                            target="_blank"
                            rel="noopener noreferrer"
                          >
                            Media {index + 1}
                          </a>
                        ))}
                    </div>
                    <span className="edit-status">
                      {post.isEdited === true
                        ? "Edited"
                        : post.isEdited === false
                          ? "Unedited"
                          : "Edit status unknown"}
                    </span>
                  </div>
                </article>
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

createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
);
