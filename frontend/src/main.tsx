import React, { useCallback, useEffect, useState } from "react";
import { createRoot } from "react-dom/client";
import "./style.css";
type Status = "open" | "in_progress" | "closed";
type Priority = "low" | "medium" | "high";
type Ticket = {
  id: number;
  title: string;
  description: string;
  status: Status;
  priority: Priority;
  createdAt: string;
  updatedAt: string;
};
type Input = Pick<Ticket, "title" | "description" | "status" | "priority">;
type Page = { items: Ticket[]; total: number; page: number; pageSize: number };
type Summary = {
  byStatus: Record<Status, number>;
  byPriority: Record<Priority, number>;
};
const statuses: Status[] = ["open", "in_progress", "closed"];
const priorities: Priority[] = ["low", "medium", "high"];
const label = (value: string) =>
  value.replace("_", " ").replace(/^./, (c) => c.toUpperCase());
const empty: Input = {
  title: "",
  description: "",
  status: "open",
  priority: "medium",
};
type Session = { expiresAt: string };

class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
  ) {
    super(message);
  }
}

async function request<T>(
  url: string,
  init?: RequestInit,
  onUnauthorized?: () => void,
): Promise<T> {
  const headers = new Headers(init?.headers);
  const method = (init?.method ?? "GET").toUpperCase();
  if (!["GET", "HEAD", "OPTIONS"].includes(method)) {
    headers.set("X-SupportDesk-CSRF", "1");
  }
  const response = await fetch(url, {
    ...init,
    headers,
    credentials: "same-origin",
  });
  if (response.status === 401) onUnauthorized?.();
  if (!response.ok) {
    const body: {
      title?: string;
      detail?: string;
      errors?: Record<string, string[]>;
    } = await response.json().catch(() => ({}));
    throw new ApiError(
      Object.values(body.errors ?? {})
        .flat()
        .join(" ") ||
        body.detail ||
        body.title ||
        `Request failed (${response.status})`,
      response.status,
    );
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}
function Login({ onLogin }: { onLogin: (session: Session) => void }) {
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  async function signIn(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const credentials = new FormData(event.currentTarget);
    setError("");
    setBusy(true);
    try {
      const session = await request<Session>("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          username: credentials.get("username"),
          password: credentials.get("password"),
        }),
      });
      onLogin(session);
    } catch (error) {
      setError(error instanceof Error ? error.message : "Could not sign in.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <main className="login">
      <section className="panel formpanel">
        <div className="brand">
          <span className="brandmark" aria-hidden="true">
            ✦
          </span>
          <strong>Support Desk</strong>
          <small>FROM ARUN AUGUSTINE</small>
        </div>
        <h1>Sign in</h1>
        <form onSubmit={(event) => void signIn(event)}>
          <label>
            Username
            <input
              name="username"
              autoComplete="username"
              maxLength={128}
              required
            />
          </label>
          <label>
            Password
            <input
              name="password"
              type="password"
              autoComplete="current-password"
              maxLength={256}
              required
            />
          </label>
          {error && (
            <div className="alert" role="alert">
              {error}
            </div>
          )}
          <button className="primary" disabled={busy}>
            {busy ? "Signing in…" : "Sign in"}
          </button>
        </form>
      </section>
    </main>
  );
}

function App() {
  const [session, setSession] = useState<Session | null | undefined>(undefined);
  const [sessionError, setSessionError] = useState("");
  const [sessionAttempt, setSessionAttempt] = useState(0);
  const clearSession = useCallback(() => setSession(null), []);

  useEffect(() => {
    const controller = new AbortController();
    setSessionError("");
    void request<Session>("/api/auth/session", { signal: controller.signal })
      .then((current) => {
        if (!controller.signal.aborted) setSession(current);
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) return;
        if (error instanceof ApiError && error.status === 401) setSession(null);
        else setSessionError("Could not check your sign-in. Try again.");
      });
    return () => controller.abort();
  }, [sessionAttempt]);

  useEffect(() => {
    try {
      sessionStorage.removeItem("support-desk.session");
    } catch {
      // The previous sign-in storage can be unavailable.
    }
  }, []);

  useEffect(() => {
    if (!session) return;
    const timer = window.setTimeout(
      clearSession,
      Math.max(0, Date.parse(session.expiresAt) - Date.now()),
    );
    return () => window.clearTimeout(timer);
  }, [session, clearSession]);

  return (
    <>
      {session === undefined ? (
        <main className="login">
          <section className="panel">
            {sessionError ? (
              <>
                <p role="alert">{sessionError}</p>
                <button
                  className="primary"
                  onClick={() => setSessionAttempt((attempt) => attempt + 1)}
                >
                  Retry
                </button>
              </>
            ) : (
              <p role="status">Checking sign-in…</p>
            )}
          </section>
        </main>
      ) : session ? (
        <TicketDesk onSignOut={clearSession} />
      ) : (
        <Login onLogin={setSession} />
      )}
      <footer>
        © Copyright All Rights Reserved By <strong>Arun Augustine</strong>
      </footer>
    </>
  );
}

function TicketDesk({ onSignOut }: { onSignOut: () => void }) {
  const api = useCallback(
    <T,>(url: string, init?: RequestInit) => request<T>(url, init, onSignOut),
    [onSignOut],
  );
  const [signingOut, setSigningOut] = useState(false);

  async function signOut() {
    setSigningOut(true);
    try {
      await api("/api/auth/logout", { method: "POST" });
      onSignOut();
    } catch (error) {
      setError(error instanceof Error ? error.message : "Could not sign out.");
      setSigningOut(false);
    }
  }

  const [tickets, setTickets] = useState<Page | null>(null);
  const [summary, setSummary] = useState<Summary | null>(null);
  const [filter, setFilter] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [form, setForm] = useState<Input>(empty);
  const [error, setError] = useState("");
  const [formError, setFormError] = useState("");
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [refresh, setRefresh] = useState(0);
  const load = useCallback(
    async (signal: AbortSignal) => {
      setLoading(true);
      setError("");
      const params = new URLSearchParams({ page: String(page), pageSize: "8" });
      if (filter) params.set("status", filter);
      if (search.trim()) params.set("search", search.trim());
      try {
        const [list, stats] = await Promise.all([
          api<Page>(`/api/tickets?${params}`, { signal }),
          api<Summary>("/api/tickets/summary", { signal }),
        ]);
        setTickets(list);
        setSummary(stats);
      } catch (e) {
        if (!signal.aborted)
          setError(e instanceof Error ? e.message : "Could not load tickets.");
      } finally {
        if (!signal.aborted) setLoading(false);
      }
    },
    [api, filter, search, page],
  );
  useEffect(() => {
    const controller = new AbortController();
    const timer = window.setTimeout(() => void load(controller.signal), 250);
    return () => {
      clearTimeout(timer);
      controller.abort();
    };
  }, [load, refresh]);
  const mutate = async (url: string, method: string, body?: Input) => {
    setBusy(true);
    setError("");
    try {
      await api(url, {
        method,
        headers: { "Content-Type": "application/json" },
        ...(body ? { body: JSON.stringify(body) } : {}),
      });
      setRefresh((v) => v + 1);
      return true;
    } catch (e) {
      setError(e instanceof Error ? e.message : "The request failed.");
      return false;
    } finally {
      setBusy(false);
    }
  };
  const create = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!form.title.trim() || form.title.trim().length > 120) {
      setFormError("Enter a title between 1 and 120 characters.");
      return;
    }
    setFormError("");
    if (
      await mutate("/api/tickets", "POST", {
        ...form,
        title: form.title.trim(),
      })
    ) {
      setForm(empty);
      setPage(1);
    }
  };
  const remove = async (ticket: Ticket) => {
    if (window.confirm(`Delete “${ticket.title}”?`)) {
      const ok = await mutate(`/api/tickets/${ticket.id}`, "DELETE");
      if (ok && tickets?.items.length === 1 && page > 1) setPage(page - 1);
    }
  };
  return (
    <>
      <header className="top">
        <div className="brand">
          <span className="brandmark">✦</span>
          <div>
            <strong>Support Desk</strong>
            <small>FROM ARUN AUGUSTINE</small>
          </div>
        </div>
        <button
          className="signout"
          type="button"
          onClick={() => void signOut()}
          disabled={signingOut}
        >
          {signingOut ? "Signing out…" : "Sign out"}
        </button>
      </header>
      <main>
        <div className="heading">
          <div>
            <p className="eyebrow">SUPPORT OVERVIEW</p>
            <h1>Keep every request moving.</h1>
            <p className="subtitle">
              A clear view of your team's support queue.
            </p>
          </div>
          <a href="#new-ticket" className="primary linkbutton">
            + New ticket
          </a>
        </div>
        <section className="stats" aria-label="Ticket summary">
          <div className="stat">
            <span>Total tickets</span>
            <strong>
              {summary
                ? Object.values(summary.byStatus).reduce((a, b) => a + b, 0)
                : "—"}
            </strong>
            <small>All requests</small>
          </div>
          {statuses.map((s) => (
            <div className="stat" key={s}>
              <span>{label(s)}</span>
              <strong>{summary?.byStatus[s] ?? "—"}</strong>
              <small>
                {s === "open"
                  ? "Needs attention"
                  : s === "in_progress"
                    ? "Being worked on"
                    : "Resolved requests"}
              </small>
            </div>
          ))}
        </section>
        <div className="layout">
          <section className="panel queue">
            <div className="panelhead">
              <div>
                <p className="eyebrow">YOUR WORKSPACE</p>
                <h2>Ticket queue</h2>
                <p>Track and manage incoming requests.</p>
              </div>
              <span className="count">{tickets?.total ?? 0} tickets</span>
            </div>
            <div className="filters">
              <label className="search">
                <span className="sr-only">Search by title</span>
                <span aria-hidden="true">⌕</span>
                <input
                  placeholder="Search tickets..."
                  value={search}
                  onChange={(e) => {
                    setSearch(e.target.value);
                    setPage(1);
                  }}
                />
              </label>
              <label>
                <span className="sr-only">Filter by status</span>
                <select
                  value={filter}
                  onChange={(e) => {
                    setFilter(e.target.value);
                    setPage(1);
                  }}
                >
                  <option value="">All statuses</option>
                  {statuses.map((s) => (
                    <option value={s} key={s}>
                      {label(s)}
                    </option>
                  ))}
                </select>
              </label>
            </div>
            {error && (
              <div className="alert" role="alert">
                {error}{" "}
                <button onClick={() => setRefresh((v) => v + 1)}>Retry</button>
              </div>
            )}
            {loading ? (
              <div className="empty" role="status">
                Loading tickets…
              </div>
            ) : !tickets?.items.length ? (
              <div className="empty">
                <strong>No tickets found</strong>
                <p>Try another search or create a new ticket.</p>
              </div>
            ) : (
              <div className="ticketlist">
                {tickets.items.map((t) => (
                  <article className="ticket" key={t.id}>
                    <div className="ticket-main">
                      <div className="ticket-meta">
                        <span>#{String(t.id).padStart(4, "0")}</span>
                        <span className={`badge ${t.priority}`}>
                          {label(t.priority)} priority
                        </span>
                      </div>
                      <h3>{t.title}</h3>
                      <p>{t.description || "No description provided."}</p>
                      <small>
                        Created{" "}
                        {new Date(
                          t.createdAt.endsWith("Z")
                            ? t.createdAt
                            : t.createdAt + "Z",
                        ).toLocaleDateString()}
                      </small>
                    </div>
                    <div className="ticket-actions">
                      <label>
                        <span className="sr-only">Status for {t.title}</span>
                        <select
                          value={t.status}
                          disabled={busy}
                          onChange={(e) =>
                            void mutate(`/api/tickets/${t.id}`, "PUT", {
                              title: t.title,
                              description: t.description,
                              priority: t.priority,
                              status: e.target.value as Status,
                            })
                          }
                        >
                          {statuses.map((s) => (
                            <option key={s} value={s}>
                              {label(s)}
                            </option>
                          ))}
                        </select>
                      </label>
                      <button
                        className="delete"
                        disabled={busy}
                        onClick={() => void remove(t)}
                      >
                        Delete
                      </button>
                    </div>
                  </article>
                ))}
              </div>
            )}
            <div className="pagination">
              <span>
                Page {page} of{" "}
                {Math.max(1, Math.ceil((tickets?.total ?? 0) / 8))}
              </span>
              <div>
                <button
                  disabled={page <= 1 || loading}
                  onClick={() => setPage(page - 1)}
                >
                  Previous
                </button>
                <button
                  disabled={!tickets || page * 8 >= tickets.total || loading}
                  onClick={() => setPage(page + 1)}
                >
                  Next
                </button>
              </div>
            </div>
          </section>
          <aside>
            <section className="panel formpanel" id="new-ticket">
              <p className="eyebrow">ADD TO THE QUEUE</p>
              <h2>Create a ticket</h2>
              <p>Capture the details and get started.</p>
              <form onSubmit={(e) => void create(e)}>
                <label>
                  Title <span>*</span>
                  <input
                    value={form.title}
                    maxLength={120}
                    onChange={(e) =>
                      setForm({ ...form, title: e.target.value })
                    }
                    placeholder="What can we help with?"
                    required
                  />
                </label>
                <label>
                  Description
                  <textarea
                    value={form.description}
                    maxLength={4000}
                    onChange={(e) =>
                      setForm({ ...form, description: e.target.value })
                    }
                    placeholder="Add a little more detail..."
                    rows={4}
                  />
                </label>
                <label>
                  Priority
                  <select
                    value={form.priority}
                    onChange={(e) =>
                      setForm({ ...form, priority: e.target.value as Priority })
                    }
                  >
                    {priorities.map((p) => (
                      <option key={p} value={p}>
                        {label(p)}
                      </option>
                    ))}
                  </select>
                </label>
                {formError && (
                  <div role="alert" className="alert">
                    {formError}
                  </div>
                )}
                <button className="primary" disabled={busy}>
                  {busy ? "Saving…" : "Create ticket"}
                </button>
              </form>
            </section>
            <section className="panel priority">
              <p className="eyebrow">AT A GLANCE</p>
              <h2>Priority breakdown</h2>
              <p>How the queue is distributed.</p>
              {priorities
                .slice()
                .reverse()
                .map((p) => (
                  <div className="priorityrow" key={p}>
                    <span>
                      <i className={p}></i>
                      {label(p)}
                    </span>
                    <strong>{summary?.byPriority[p] ?? "—"}</strong>
                  </div>
                ))}
            </section>
          </aside>
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
