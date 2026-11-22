import type { FormEvent } from "react";
import { channels } from "./channels";
import type { Filters } from "./types";

type Props = { draft: Filters; setDraft: (filters: Filters) => void; submit: (event: FormEvent<HTMLFormElement>) => void };

export function SearchFilters({ draft, setDraft, submit }: Props) {
  return (
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
  );
}
