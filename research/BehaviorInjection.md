# Behavior injection for the PACE LearnEngine MCP server

Labels used below:
- **[Verified]** means I checked it in the official docs or the spec (URLs given) or in our own repo.
- **[Observed]** means I saw it happen in a live session.
- **[Inference]** means it is my own judgment and has not been tested.

## 1. Is "behavior injection" a real term?

It is not an established term. [Inference, based on what the search found.] The names people actually use are:

- **Server instructions.** The MCP spec field that does this job. The spec calls it guidance that "MAY be added to the system prompt" and "a hint to the model". [Verified: https://modelcontextprotocol.io/specification/2025-11-25/schema]
- **Tool descriptions as prompts.** Anthropic treats tool descriptions as prompt engineering and credits description changes with measurable gains. [Verified: https://www.anthropic.com/engineering/writing-tools-for-agents]
- **Steering**, "steer agents" in Anthropic's wording. A tool's results and errors push the agent toward the next action. [Verified, same URL]
- **Context engineering.** The general practice of deciding what text reaches the model and when. [Inference: common industry usage]

The adversarial terms are **prompt injection** and, for MCP, **tool poisoning**: hidden instructions in tool metadata that make the model act against the user (Invariant Labs, https://invariantlabs.ai/blog/mcp-security-notification-tool-poisoning-attacks; cited from memory and not re-fetched).

Our use is legitimate for three reasons:
- the student installed PACE on purpose;
- the guidance serves the student's own goal;
- nothing is hidden, and nothing tries to override the host's rules or the user.

Anthropic's directory review draws this line too. It rejects tool descriptions that "tell Claude to behave in ways unrelated to the tool's function, attempt to override system instructions", or "interfere with Claude calling other tools". It also says: "Describe what the tool does, and don't tell Claude how to behave." [Verified: https://claude.com/docs/connectors/building/review-criteria.md]

The practical rule that follows:
- Tool descriptions say what a tool does and when to call it. "Call after every learner message" is a when rule, so it belongs there.
- The teaching style goes in server instructions, prompts or skills, not in tool descriptions.

## 2. Every lever an MCP server has

| Mechanism | Reaches the model? | When | Reliability and limits | User action needed |
|---|---|---|---|---|
| `instructions` (initialize result) | Yes in Claude Code [Verified]. In claude.ai and Desktop I could not confirm from Anthropic docs [unverified]. | Session start, as system context | Claude Code cuts it at 4,096 characters (2,048 before v2.1.296); `CLAUDE_CODE_MAX_MCP_DESCRIPTION_LENGTH` changes this [Verified: https://code.claude.com/docs/en/mcp]. It is guidance only and fades over long chats [Inference]. | None |
| Tool name, description, parameter descriptions | Yes | When the tool list loads. Under Claude Code tool search, only names and server instructions load upfront; descriptions arrive when Claude searches [Verified, same URL] | 4,096 characters if loaded upfront, 16,384 if loaded through search (Claude Code). Put the key words first. | None |
| `_meta["anthropic/alwaysLoad"]` | Makes a tool load upfront in Claude Code | Session start | Claude Code only [Verified, same URL] | None |
| Tool annotations (`title`, `readOnlyHint`, `destructiveHint`) | Mostly host UI. They set permissions: "Read-only tools can run without per-call confirmation, and destructive tools always prompt." | Every call | The spec says clients must treat annotations as untrusted unless the server is trusted [Verified: spec tools page, review criteria]. | The user may have to approve calls |
| Tool results (text plus `structuredContent`) | Yes, every time, right where the next decision is made | After each call | Up to about 150,000 characters in claude.ai and Desktop, 25,000 tokens in Claude Code [Verified: https://claude.com/docs/connectors/building/index.md]. This is the strongest steering point. | None |
| Tool errors | Yes | On failure | Anthropic recommends "specific and actionable" errors [Verified]. | None |
| Prompts | Yes, as a user message, which carries high weight | Only when the user picks one. The spec calls prompts "user-controlled" [Verified: spec prompts page]. Claude supports prompts [Verified: building index]. Claude Code shows them as `/server:prompt`. | Very reliable once picked, but someone has to pick it | Yes |
| Resources | Only if the user attaches one or the model reads it | On demand | Supported, but subscriptions are not [Verified] | Usually yes |
| Elicitation | No. It is a form shown to the user. | When the server asks | Claude Code supports it. For claude.ai and Desktop it is not documented. [Verified for Code, unknown elsewhere] | Yes |
| Sampling | Not applicable | | Claude does not support it [Verified]. It is also deprecated in the spec by SEP-2577 [Verified: https://modelcontextprotocol.io/seps/2577-deprecate-roots-sampling-and-logging.md]. | |
| MCP Apps (UI in the chat) | Indirectly | When a tool renders one | Supported in Claude Desktop and claude.ai [Verified: connectors docs]. Whether an app can push context into the model needs testing [Inference]. | The user interacts with it |

Spec change worth knowing: in the newest spec (2026-07-28), `instructions` lives on the `server/discover` result, described as text clients may put "in a system prompt". The spec now adds that instructions "should not duplicate information already in tool descriptions". [Verified: https://modelcontextprotocol.io/specification/2026-07-28/schema] Our C# SDK still sets it on initialize. That is fine for now.

## 3. What our own repo shows

**How PACE does it now** [Verified, in `C:\Users\Overlord\OneDrive\Desktop\Projects\PaceLearning\Projects\Pace\src\Hosts\Pace.Mcp.Api\McpApi.cs` and `LearnerTools.cs`]:
- `ServerInstructions` is one 4,370 character string in 9 paragraphs, covering learner memory, courses, personal lessons, projects and boards.
- The "call get_learner_brief first" rule appears in paragraph 3 and again in the `get_learner_brief` description, which says "Call it at the start of any conversation about learning, before teaching...". The duplication is good.
- `write_learner_note` is driven by a long list of triggers in both the instructions and the description. That description is about 2,670 characters.
- `LearnerPrompts.cs` adds three prompts in the student's voice (`get_to_know_me`, `wrap_up_session`, `draw_with_me`). Each one starts with "Call get_learner_brief".
- Of 104 tools, few set `Title` or `ReadOnly`, and LearnerTools sets none. Directory submission requires a `title` and `readOnlyHint` or `destructiveHint` on every tool.

**Truncation** [Observed in this session]:
- The "claude.ai Pace" instructions reached this Claude Code session but stopped at "Where the brief ... [truncated]".
- That cut falls at about character 4,080 of the 4,370 character text, which matches the documented 4,096 character cap.
- What was lost: the board rules "ask the student to draw or predict first", "never change or erase what the student drew" and "end with a learner note".
- Rules at the end are the first to go. Under the older 2,048 character cap, everything after the get_learner_brief paragraph would have been lost.
- Claude Code also cuts the instructions of the last server silently when several servers are configured (community report, https://claudeissues.com/issue/43474-mcp-server-instructions-are-silently-truncated-when-multiple-servers-are-configu). [unverified]

**Lessons:**
- Put the most important rule first.
- Stay well under 2,048 characters, because an older client or a stricter host may cut there.
- Repeat each critical rule in the description of the tool it concerns.

## 4. Patterns that make calls happen at the right moment

The patterns themselves are my design judgment. Where a pattern rests on a documented fact, it says so.

1. **Make the per turn call worth making.** If `report_turn` returns the move the model should make next, calling it helps the model answer. A model skips chores, not useful lookups. This is the most important idea here.
2. **The result names the next call.** End each result with a short `next` field, for example "After the student's next message, call report_turn with turn 5." The rule then shows up again every turn, right where the next decision happens, instead of sitting once in system context that fades.
3. **Short imperative numbered rules first.** Something like "1. Call X when Y." Background goes after the rules, never before.
4. **Say each rule twice.** Once in the instructions and once in the tool description, because tool search in Claude Code hides descriptions until searched. Mark `start_session` and `report_turn` with `anthropic/alwaysLoad`.
5. **Make skipping visible.** Have `report_turn` take a `turn` number the model counts. A jump from 3 to 6 tells the server turns were missed. The result can then say "Turns 4 and 5 were not reported. Report this message and continue." This is soft, but the model then sees its own gap.
6. **Be forgiving with state.** If `report_turn` arrives with no open session, open one automatically and say so in the result. Do not throw "call start_session first". An error costs an extra round trip and the model may give up. Reserve hard errors for bad signal ids, and list the valid ids in the error message.
7. **Keep the call cheap.** Small input (ids only), fast response, and no permission prompt on every turn. Without "always allow", a write tool may ask for approval each time. Tell the student once to choose "Always allow" for report_turn. Read only tools skip the prompt [Verified]. I do not know how claude.ai treats a tool that is neither read only nor destructive, so test it.
8. **One user entry point.** A `start_learning` prompt in the student's voice, like our existing prompts, puts the rules into the conversation as a user message. That is the most reliable start. The server instructions are the fallback for students who just start talking.

**What typically fails** [Inference, from general agent behavior and our transcripts]:
- Calls get skipped on short replies ("ok", "thanks").
- Calls stop after many turns, once the instructions are far back in context.
- The model calls report_turn after it replies instead of before, so the move is wasted.
- The model invents signal ids.
- The model tells the student about the odds.
- The model batches several turns into one call near the end.
- With many tools loaded (PACE has 104), the model forgets that a tool exists.

## 5. What a server cannot do, and the alternatives

From an MCP server we cannot:
- write the system prompt;
- force a tool call;
- see the conversation;
- know how many messages the student sent;
- run code between turns.

Every call is the model's choice. So compliance can be raised but never guaranteed.

| Alternative | What it gives | Cost |
|---|---|---|
| Claude Projects custom instructions | Persistent instructions for every chat in the project, close to system level | The student must create the project and paste the text. Easy to get wrong, and it does not travel with the connector. |
| Skills in a plugin with our connector | Anthropic's official answer: "skills are where you put the sequence of steps, the defaults". Skills load in claude.ai chat, Cowork and Claude Code. A plugin can bundle the connector and the skills. [Verified: https://claude.com/docs/connectors/building/what-to-build.md, https://claude.com/docs/plugins/platform-support.md] | The student installs a plugin. A skill loads only when Claude judges it relevant. Hooks, which could enforce per turn calls, are ignored in chat [Verified]. |
| Hooks (Claude Code and Cowork only) | A deterministic script on every user prompt that could call report_turn itself | Not available in chat. Students on Desktop chat would not get it. |
| Our own client on the Claude API or Agent SDK (in PACE itself) | Full control. Our code, not the model, calls report_turn every turn, for example with a small model that maps the message to signal ids, and then puts the move into the system prompt. Reliability is effectively 100 percent. | We pay for tokens, build a chat UI, and lose the "bring your own Claude" story. |
| MCP App for the session | UI inside the chat that can show progress and start the session | Still model driven. Unproven for per turn reporting. |

**Recommendation** [Inference]:
- Ship the MCP path with the patterns above and a skill in a PACE plugin.
- Treat host compliance as a measured number, not an assumption.
- Keep the engine independent of the host so a PACE chat on the Claude API can drive it deterministically later.

## 6. Concrete proposal for LearnEngine

### Server instructions (939 characters)

```
PACE tracks how a student is learning so you can teach them well. Three rules come before anything else.
1. When the student starts learning something, call start_session before you teach. Keep the sessionId.
2. After every message the student sends in that session, call report_turn before you reply, even for short messages like "ok". Send only signal ids from the list start_session returned that describe the message. Never send the student's words. If no signal fits, send an empty list.
3. Read the result of report_turn before you reply. When clear is true, use its move. When clear is false, teach as you normally would.
How to teach: ask the student to predict or explain before you explain. Give one idea at a time. Prefer a hint or a question to an answer. Never show the student odds, scores or labels about them.
If a tool says there is no open session, call start_session and go on. When the student stops, call end_session.
```

If this has to share space with the existing PACE instructions, this block goes first and the current text follows. The board paragraph is moved into a skill or into the board tools' descriptions.

### start_session description

```
Opens a learning session and returns its sessionId, the signal ids you may report, and the first move. Call it once when the student starts learning something, before you teach, with the space and the topic. Then call report_turn after every message the student sends.
```

Annotations: `title` "Start learning session", `readOnlyHint` false, `destructiveHint` false, `alwaysLoad` true.

Result:
- `sessionId`;
- `signals`: id plus a one line meaning, kept to about 20 ids;
- `move`;
- `next`: "After the student's next message, call report_turn with turn 1."

### report_turn description

```
Reports one student message as signal ids and returns what to do next. Call it after every message the student sends in a session, before you reply, even short ones. signals are ids from start_session that describe the message, never its words; send an empty list when none fit. turn counts the student's messages in this session, starting at 1.
```

Parameters: `sessionId`, `turn`, `signals` (array of ids), and optional `lastMoveId`, the move you actually used, so we learn whether moves were followed.

### report_turn result (structuredContent plus the same JSON as text)

```json
{
  "turn": 5,
  "clear": true,
  "move": { "id": "ask_prediction", "do": "Ask the student to predict the output before you show it." },
  "state": { "summary": "probably confused about loop bounds", "confidence": "high" },
  "missedTurns": [],
  "next": "After the student's next message, call report_turn with turn 6."
}
```

- When `clear` is false: `move` is null and `do` reads "Teach as you normally would."
- `state.summary` is for the model only. The instructions already forbid showing it to the student.
- Unknown signal ids get an error that lists the valid ids.
- A missing session opens a new one and says so in `next`.

### How to measure compliance

1. **Server metrics, from day one:**
   - share of conversations that call get_learner_brief but never start_session;
   - reported turns per session;
   - the `missedTurns` rate (gaps in `turn`);
   - share of turns with empty signals;
   - how often `lastMoveId` equals the move we sent (moves followed).
2. **Signal consistency.** After an `ask_prediction` move, the next turn should often carry a `made_prediction` signal. A low match rate means moves are ignored.
3. **Scripted evaluation on the API.** A simulated student (a Claude instance with a persona and a fixed script of N messages) talks to a tutor model that gets our instructions and tools as MCP tools. Count report_turn calls against N, check that each call comes before the reply, and check the ids are valid. Run it per instructions draft and per model. This only approximates Desktop, but it catches regressions. [Inference; follows Anthropic's evaluation advice in the tools post]
4. **Manual runs on the real hosts.** About 10 scripted conversations each in Claude Desktop, claude.ai and Claude Code with a test account. Check the server log against the transcript. This also answers the open question of whether claude.ai applies `instructions`. A quick check: put a harmless marker sentence in the instructions and see whether the model knows it.
5. **Student check.** The wrap_up prompt can show "PACE recorded N of your messages this session" so the student can see when tracking failed.

## Sources

- https://modelcontextprotocol.io/specification/2025-11-25/basic/lifecycle
- https://modelcontextprotocol.io/specification/2025-11-25/schema
- https://modelcontextprotocol.io/specification/2026-07-28/schema
- https://modelcontextprotocol.io/specification/2026-07-28/server/tools
- https://modelcontextprotocol.io/specification/2026-07-28/server/prompts
- https://modelcontextprotocol.io/seps/2577-deprecate-roots-sampling-and-logging.md
- https://code.claude.com/docs/en/mcp
- https://claude.com/docs/connectors/building/index.md
- https://claude.com/docs/connectors/building/review-criteria.md
- https://claude.com/docs/connectors/building/submission.md
- https://claude.com/docs/connectors/building/what-to-build.md
- https://claude.com/docs/plugins/platform-support.md
- https://support.claude.com/en/articles/11503834-building-custom-connectors-via-remote-mcp-servers
- https://www.anthropic.com/engineering/writing-tools-for-agents
- https://claudeissues.com/issue/39106-docs-mcp-docs-do-not-mention-the-2kb-cap-on-tool-descriptions-and-server-instruc
- https://claudeissues.com/issue/43474-mcp-server-instructions-are-silently-truncated-when-multiple-servers-are-configu
- https://www.developersdigest.tech/mcp-servers/clients/claude-desktop
- https://invariantlabs.ai/blog/mcp-security-notification-tool-poisoning-attacks (from memory)

Repo files read:
- C:\Users\Overlord\OneDrive\Desktop\Projects\PaceLearning\Projects\Pace\src\Hosts\Pace.Mcp.Api\McpApi.cs
- C:\Users\Overlord\OneDrive\Desktop\Projects\PaceLearning\Projects\Pace\src\Hosts\Pace.Mcp.Api\LearnerTools.cs
- C:\Users\Overlord\OneDrive\Desktop\Projects\PaceLearning\Projects\Pace\src\Hosts\Pace.Mcp.Api\LearnerPrompts.cs