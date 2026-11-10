# ADR-0006: Frontend choice

Status: Accepted
Date: 2026-11-06

## Context and Requests <!-- HUMAN -->

- R1: The poll page shows live-updating charts from a Server-Sent Events stream.
- R2: The frontend is written in TypeScript.
- R3: It builds to static files served by nginx. No server-side rendering.
- R4: It is easy to learn for .NET developers new to frontend work.
- R5: AI coding agents know it well.
- R6: Local development starts in seconds.
- Human decision: React + Vite + TypeScript.
  Record the comparison from the canvas as the options.

## Out of Scope <!-- HUMAN -->

- Server-side rendering frameworks (for example Next.js)
- A mobile app
- A design system or component library of our own
- Multiple languages

## Options and Decision <!-- AI PROPOSES -->

The framework rows are the canvas comparison, scored 1–5 on R1–R6 in that order. The total is the unweighted sum, maximum 30. Weighting those rows 6 down to 1 gives React 99, Vue 94, Svelte 87, Angular 86, and Blazor WebAssembly 54.

| Option | Scores for R1–R6, then total | Fit |
| --- | --- | --- |
| React + Vite | 5, 5, 5, 3, 5, 5 = 28 | Human decision. Strongest live-chart path and the stack agents write most reliably. `vite build` emits static files. |
| Vue + Vite | 4, 5, 5, 4, 4, 5 = 27 | Closest JavaScript shape to Razor. Live-chart samples are split across wrappers. |
| Svelte + Vite | 4, 4, 5, 4, 3, 5 = 25 | Small components and a static Vite build. Agents still mix Svelte 4 with Svelte 5, and the chart libraries are thinner. |
| Angular | 4, 5, 5, 2, 4, 3 = 23 | TypeScript is mandatory and dependency injection matches ASP.NET. The largest surface to learn. The dev server is fast; the project shape is the weight. |
| Blazor WebAssembly | 2, 1, 4, 5, 2, 2 = 16 | Stays in C# and Razor, and the publish output is static. It is not TypeScript. Charts and EventSource go through JavaScript interop. |

### Chart library

The poll page reads the results stream ADR-0003 already defines: one snapshot per poll per second, current totals and votes per second, and the page opens a new stream itself when that API replica stops. ADR-0003 leaves the full vote history out, so the chart replaces the latest totals. It does not keep older snapshots.

| Option | What the page does with a snapshot | Fit |
| --- | --- | --- |
| 1. Recharts | A React bar chart receives a new array of votes per option. Animation stays off. | Meets R1, R2, R4, and R5. Six bars at one update per second do not need a canvas engine. |
| 2. react-chartjs-2 | Chart.js draws the bars on a canvas. Updates go through the chart instance. | Agents know Chart.js. The mutable instance fights the React state a new .NET developer just learned. |
| 3. echarts-for-react | An ECharts option object describes the bars. | Strong live charts. The option object is the most to learn, which misses R4 for this page. |

### API client

Sign-in, polls, voting, the metrics poll, admin edits, and simulator start and stop are JSON calls through nginx. The results stream stays the browser EventSource from ADR-0003. The browser never calls Ruvio or Postgres. The OpenAPI document is `docs/api/openapi.yaml`.

| Option | How the client code is created | Fit |
| --- | --- | --- |
| 1. Hand-written fetch | One TypeScript module of `fetch` calls, plus EventSource for the results stream. | Meets R2, R4, R5, and R6. Types can drift from the contract. |
| 2. Types from `docs/api/openapi.yaml` | A script writes the TypeScript types from that file. EventSource for the results stream stays hand-written. The generated file is committed. | Types follow the contract. `vite` does not wait on a generator. The stream stays the ADR-0003 client. |
| 3. NSwag TypeScript client | NSwag writes a class client from that same document. | Familiar on a .NET team. The generated class is large, and agents edit it by mistake. The results stream stays hand-written. |

Decision: React + Vite + TypeScript, as the human decision. Recharts for the poll chart. Generate the TypeScript types from `docs/api/openapi.yaml` and commit them. EventSource for the results stream stays hand-written. Calls made as the signed-in user send that user's JWT in the `Authorization: Bearer` header. The public results stream stays open without it, as FR-8 and ADR-0004 already allow.

1. R1. The poll page applies each SSE snapshot to a Recharts bar chart of the current votes per option, with animation off, and shows the votes-per-second figure from that same snapshot. When the stream ends, the page opens it again, as ADR-0003 requires. The metrics panel keeps polling once a second through the fetch module, as ADR-0005 requires.
2. R2. Components, the fetch module, and the chart props are TypeScript.
3. R3. `vite build` writes the static files nginx serves. The dev server and the production build do not render on a server. Next.js and other server renderers stay unused.
4. R4. The learned surface is function components, the generated types, a hand-written EventSource, and Recharts props. There is no component library of our own.
5. R5. React, Vite, Recharts, and the generated types are the common path. The recorded totals are React 28, Vue 27, Svelte 25, Angular 23, and Blazor WebAssembly 16.
6. R6. `vite` serves the app without waiting for the API process. The generated types are already committed, so dev start does not run the generator.

## Consequences and Risks <!-- AI PROPOSES -->

- FR-9 now includes the live-updating chart of the totals. Exporting results stays out of scope in docs/requirements.md.
- A votes-per-second line kept on the client would be history. ADR-0003 does not replay history after reconnect, so this decision shows the latest votes-per-second number beside the bar chart.
- ADR-0003 sends at most one snapshot per poll every 1 second. Recharts animation stays off so a snapshot replaces the bars.
- TypeScript types for the JSON API are generated from `docs/api/openapi.yaml` and committed. The results EventSource stays hand-written.
- The signed dev token from ADR-0004 is a stateless JWT. The JSON calls send it as `Authorization: Bearer`. Sign-in itself sends only the user name. A token for a name in `Admin__Users` includes `role=admin`.
- ADR-0005 leaves browser end-to-end tests out of scope. The tests below use a snapshot fixture and the build, not a browser.
- This accepted record answers architecture question 1. The resolved list is in architecture.md.
- JSON calls go through nginx. The BFF routes `/admin/simulations/*` to the Simulator and checks `role=admin` there (ADR-0004). The API does not handle those routes.

## Resolved questions

- Q1. React, Vite, and TypeScript, with Recharts for the poll chart. Recorded in this ADR.
- Q6. The BFF delivers simulator start and stop. Recorded in ADR-0004.
- The JSON types are generated from `docs/api/openapi.yaml`. The results EventSource stays hand-written.

## Verification <!-- AI PROPOSES, HUMAN CHECKS -->

| Request | Test name | What the test proves |
| --- | --- | --- |
| R1 | PollPage_ChartReplacesTotalsFromSseEvent | One SSE snapshot sets the bar for each option and the votes-per-second figure. A second snapshot replaces both. The chart does not keep the first snapshot. |
| R1 | PollPage_ReopensResultsStreamWhenItEnds | When the results response ends, the page opens a new EventSource on the same public results URL. |
| R2 | Frontend_TypecheckPasses | The frontend typecheck passes for the React components, the generated API types, and the Recharts props. |
| R3 | Frontend_ViteBuildWritesStaticFiles | `vite build` writes static files nginx can serve, and the project has no server-render entry. |
| R4 | Frontend_TypesGeneratedFromOpenApi | TypeScript types for the JSON API are generated from `docs/api/openapi.yaml`. The results stream stays a hand-written EventSource. |
| R4 | Frontend_SurfaceIsReactFetchAndRecharts | App code is React function components in TypeScript, signed-in calls send the JWT as `Authorization: Bearer`, the poll chart is Recharts, and the app does not depend on a private component library. |
| R5 | Frontend_DependenciesStayOnTheRecordedStack | The frontend dependencies used for UI, build, and charts are React, Vite, and Recharts. JSON types come from `docs/api/openapi.yaml`. |
| R6 | ViteDev_AcceptsConnectionsWithinFiveSeconds | Starting Vite accepts a connection within 5 seconds. That start uses the committed types and does not wait for the API process. |

Every R must have at least one test.
