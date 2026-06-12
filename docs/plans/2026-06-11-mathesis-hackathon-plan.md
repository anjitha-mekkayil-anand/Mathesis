# Mathesis — Agents League AISF Submission Plan
*Reasoning Agents track · Deadline Sun Jun 14, 2026 · Created 2026-06-11*

**Mathesis** (Greek: the act of learning) — an enterprise learning system that builds
certification readiness for teams: grounded study plans, cited assessments, and a
manager who approves every path. Sibling project to Pronoia (which remains a personal
project after the organizer confirmed the track scenario is mandatory).

---

## Scenario compliance

Organizer ruling (Discord, 2026-06-10): Reasoning Agents submissions must be based on
one of the two starter packs — **enterprise learning system** or a D&D-style game.
Mathesis implements the first, mapped directly to the Challenge details baseline flow:

| Spec requirement | Mathesis |
|------------------|----------|
| Multi-agent system aligned to scenario | Curator → Planner → Assessor → Manager Insights |
| Microsoft Foundry (UI or SDK) | Azure AI Foundry, Mistral-small via SDK |
| ≥1 Microsoft IQ layer | **Foundry IQ** — Azure AI Search index `mathesis-learning-kb`, cited retrieval |
| Reasoning + multi-step decisions across agents | Distinct tool surfaces per agent; readiness loop |
| External tools / MCP | Mathesis.Mcp server (stdio); stretch: Microsoft Learn MCP server |
| Synthetic data and documents ONLY | All learner/cert/KB data synthetic (shapes from spec examples) |
| Demoable + agent interactions explained | Console narration `[Curator] → [Planner] → [Assessor]` + dashboard |
| Documentation: roles, orchestration, tools, data | README compliance map |
| RAI: human oversight in important decisions | **Manager approval gate — no plan activates without a human** |

Highly Valued Extras targeted: observability (console trace + SQLite audit),
reasoning patterns (Planner–Executor + human verifier), RAI fallbacks (grounding
fail-open), hosted deployment story (What's-next).

---

## Architecture

```
learners.json + certifications.json        mathesis-learning-kb (Azure AI Search)
        │                                           │ Foundry IQ cited retrieval
        ▼                                           ▼
Readiness Calculator (deterministic pre-filter — no LLM)
        │ readiness gaps
        ▼
[1] LEARNING PATH CURATOR — maps role + target cert to content, cited from KB
        ▼
[2] STUDY PLAN GENERATOR — schedule from work-activity signals → records pending plan
        ▼
[3] ASSESSMENT AGENT — cited questions from KB, readiness verdict
        ▼
[4] MANAGER INSIGHTS — team readiness summary, risk areas
        ▼
   HUMAN GATE: manager approves/rejects each study plan
   (Blazor dashboard — nothing activates without approval)
```

- **Readiness score** (deterministic pre-filter): 55% domain self-ratings + 25% study
  hours vs recommended + 20% practice score.
- **Tool surface = role boundary**: each agent gets only its role's tools; no agent
  can certify or activate a plan — only the manager.

### Mathesis.Mcp tools
| Tool | Type | Used by |
|------|------|---------|
| `list_learners` | read | Planner, Manager Insights |
| `get_learner_readiness` | read | Curator, Assessor, Manager Insights |
| `get_certification` | read | Curator, Assessor |
| `get_learning_history` | read | Planner, Manager Insights |
| `propose_study_plan` | write | Planner |
| `record_assessment` | write | Assessor |
| `recommend_next_step` | write | Assessor |

---

## Synthetic data (shapes from the challenge details examples)
- `learners.json` — EMP-001-style: meeting/focus hours, preferred slot, role, domain ratings
- `certifications.json` — AZ-204, AZ-400, DP-203: domains + weights, recommended hours, thresholds
- `learning-history.json` — prior synthetic outcomes grounding the Planner
- `learning-kb/` — 7 synthetic docs: study guides ×3, study patterns, workload insights,
  team learning report, assessment writing guide

---

## Build record

| Day | Work |
|-----|------|
| Thu Jun 11 (night 1) | Full scaffold, datasets, KB docs, MCP server, agents, orchestrator, two human gates — live-verified all three readiness bands |
| Thu Jun 11 (day 2) | Azure AI Search seeded; grounded citations verified; dashboard; README; media pipeline (Playwright + ffmpeg + edge-tts); dark theme |
| Fri Jun 12 | YouTube uploads, project page finalization, submission |
