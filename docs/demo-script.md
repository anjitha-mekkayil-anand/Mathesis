# Mathesis — Demo Script & 3-Minute Video Plan

*Talk track for the submission video and any live walkthrough.*

---

## Pre-flight (T-20 minutes before recording)

- [ ] **Run one FULL grounded pipeline pass end-to-end** (`EMP-003`) — confirm:
  - `[Foundry IQ] grounded: index 'mathesis-learning-kb'` prints at startup
  - a source document is cited in the curated path / plan rationale / questions
  - A passing build is not a passing demo; exercise the real code path.
- [ ] Run `EMP-001` once so a **pending plan sits in the approval queue** for the dashboard segment
- [ ] Dashboard running at `http://localhost:5221`, page loaded
- [ ] Terminal font large, window sized for recording; README open in a browser tab
- [ ] Close anything personal (mail, Teams, other tabs)

---

## Video plan (2:00 — HARD CAP per official submission email, Jun 11)

⚠ **No background music or any third-party/copyrighted material** — voice-over only
(or royalty-free with proof). Official rule from the submission reminder email.

| Time | Show | Say |
|------|------|-----|
| 0:00–0:15 | Cover card → README compliance map | "Mathesis — a multi-agent enterprise learning system for team certification programmes: the Reasoning Agents scenario end to end. Four agents on Microsoft Foundry, Foundry IQ grounding — and two human gates." |
| 0:15–0:30 | `dotnet run -- --readiness` | "First, a deterministic readiness score — no LLM in the hot path. Four learners, four situations; this routing decides how much agent work each one needs." |
| 0:30–1:10 | `dotnet run -- EMP-003` (interactive) — speed up dead air in edit | "EMP-003 is borderline. The Curator maps her weak domains to content, **citing source documents from the Foundry IQ knowledge base** — Azure AI Search underneath. The Planner reads her real work signals and builds a schedule that fits her week. The plan records as *pending* — it can't activate itself. First human gate: ready to be assessed?" — type `y` — "The Assessment Agent generates cited questions and an honest verdict against the pass threshold." |
| 1:10–1:45 | Dashboard | "Everything lands here. Team readiness — note the capacity flag: heavy meeting load is a manager's scheduling problem, not a learner failure. And the second human gate: the approval queue. The agents have no approve tool — this button is the only way a plan activates." Click **Approve**; show it move to Recently Decided. |
| 1:45–2:00 | Architecture diagram | "A deterministic pre-filter, four specialised agents, cited knowledge, two human gates. The agents reason; people decide. Repo linked below." |

**Recording tips:** record terminal and dashboard as separate clips and stitch
(Clipchamp, no music track); speed up agent "thinking" gaps 2–4× in edit; total ≤2:00
— re-record a clip rather than living with a flubbed line.

---

## Judge Q&A bank

**"Why two human gates?"**
> "The suggested architecture has one — the learner deciding when to be assessed. I kept it and added a second, because a study plan touches someone's career and workload. The manager gate isn't a prompt instruction: there is no approve tool in the MCP server. The agents cannot activate plans even if they hallucinate the intent. The Responsible AI requirement — human oversight in important decisions — is enforced by the tool surface, not by a disclaimer."

**"How is the multi-step reasoning visible?"**
> "Three ways. The console narrates every hop and handoff. Each agent's output is the next agent's input — curated path feeds the planner, the plan feeds the gate, the gate feeds the assessor. And everything is persisted to SQLite with timestamps, so the approval queue doubles as an audit log of the reasoning chain."

**"What about latency and cost?"**
> "The deterministic readiness calculator runs first, so the LLM is never in the hot path. A Ready learner costs one agent pass instead of four — the Curator and Planner never run. One Foundry IQ retrieval per learner is shared by every agent in the pass. And there's a zero-credential mode: the readiness report runs with no LLM at all."

**"What's your observability story?"**
> "Console narration per hop, a grounding status line on every startup — grounded or ungrounded, never silent — and SQLite persistence of every plan, assessment, and decision. If retrieval dies mid-run, the system warns loudly and continues ungrounded rather than failing: a dead knowledge layer never kills an analysis."

**"Why code-first instead of Foundry portal agents?"**
> "Deliberate: agents versioned in git, unit-testable, with the orchestration logic readable in the repo. The trade-off is no free portal playground. Hosted Agents on Foundry Agent Service is the natural next step — the orchestrator boundary makes it a lift-and-shift."

**"Why .NET and not Python?"**
> "The validity requirements ask for Microsoft Foundry via UI or SDK, or the Agent Framework — no language. Twenty years of .NET, and the agent engine was already proven in C#. It also shows agent patterns aren't Python-only."

**"Is the data real?"**
> "Fully synthetic, by requirement and by design — fabricated learners, outcomes, and knowledge documents, stated explicitly in the README. The certification IDs are recognisable labels but the domain definitions are simplified demo blueprints."

**"Where did this come from in three days?"**
> "The agent engine — the LLM loop, MCP client, grounding service, fail-open design — was built and verified in a sibling project, Pronoia, a predictive-maintenance system. When the track scenario was confirmed as mandatory, the engine was rebuilt into this scenario overnight. The scenario changed; the architecture held."

---

## Failure modes

| Issue | Cause | Fix |
|-------|-------|-----|
| `ApiKey is empty` | user secrets missing | `dotnet user-secrets set "LearningAgents:AzureFoundry:ApiKey" "<key>" --project src/Mathesis.Runner` |
| `[Foundry IQ] ungrounded` when it should be grounded | Search secrets missing | set `FoundryIq:SearchEndpoint` + `FoundryIq:ApiKey` secrets, re-run |
| `[Foundry IQ] WARNING: retrieval failed` | Search unreachable | demo continues ungrounded by design — fix endpoint/key after |
| Empty approval queue on dashboard | fresh db | run `EMP-001` once before the dashboard segment |
| Gate prompt doesn't appear | input redirected / `--all` | run a single learner from a real interactive terminal |
