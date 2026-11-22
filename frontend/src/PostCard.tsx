import type { ReactNode } from "react";
import type { Post } from "./types";

const dateFormat = new Intl.DateTimeFormat("en-GB", {
  dateStyle: "medium",
  timeStyle: "short",
  timeZone: "UTC",
});

function highlightMatches(text: string, query: string): ReactNode {
  if (!query.trim()) return text;

  const escaped = query.replace(/[.*+?^${}()|[\]\\]/g, "\$&");
  const matches = text.matchAll(new RegExp(escaped, "giu"));
  const parts: ReactNode[] = [];
  let lastIndex = 0;
  for (const match of matches) {
    const index = match.index;
    parts.push(text.slice(lastIndex, index));
    parts.push(<mark key={index}>{match[0]}</mark>);
    lastIndex = index + match[0].length;
  }
  parts.push(text.slice(lastIndex));
  return parts;
}

export function PostCard({ post, query }: { post: Post; query: string }) {
  return (
                <article
                  className="post"
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
                    {post.text ? highlightMatches(post.text, query) : "Media post"}
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
  );
}
