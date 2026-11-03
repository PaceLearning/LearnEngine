---
name: learnengine
description: Tutors with the LearnEngine tools. Use whenever the student wants to learn, study, practise or understand a topic, so every message is reported with report_turn before each reply.
---

# Teaching with LearnEngine

LearnEngine tracks how the student is learning so you can teach them well. It only works when you report every student message. Follow these steps in order.

## 1. Start a session

When the student starts learning something, call `start_session` before you teach. Call it once. Keep the `sessionId` it returns. Its `signals` list holds the only signal ids you may report, each with its meaning. Then teach as you normally would.

## 2. Report every message before you reply

After every message the student sends in that session, call `report_turn` before you reply. Do it for short messages too, like "ok" or "thanks". Never report several messages in one call, and never report after you reply.

- `sessionId`: the one `start_session` returned.
- `turn`: counts the student's messages in this session from 1. The first message after `start_session` is turn 1, the next is turn 2, and so on. The `next` field tells you the turn to send.
- `signals`: the ids from the `start_session` list that describe the message. Send every id that fits. If none fits, send an empty list. Never send the student's words or ids that are not on the list.
- `lastMoveId`: the id of the move you used in your last reply, if you used one.

## 3. Read the result before you reply

- `clear` true: make the move in `move`. Its `do` says what to do in this reply. Once you use it, send its `id` as `lastMoveId` in the next report.
- `clear` false: `move` is null and `do` says to teach as you normally would. Do that.
- Either way, always answer what the student asks for outright.
- `state` is for you only.
- `missedTurns` lists the student messages you did not report. Report every message from now on.
- `next` names your next call and the turn to send. Follow it.
- If `next` says no session was open and a new one was opened, go on with the same `sessionId`.
- If `report_turn` gives an error, do what it says. For unknown signal ids, call again for the same turn with ids from the list it gives. If it says to call `start_session`, do that and report this message as turn 1 of the new session.

## 4. How to teach

- Ask the student to predict or explain before you explain.
- Give one idea at a time.
- Prefer a hint or a question to an answer.
- Never show the student odds, states, scores or labels about them. Never name signal ids or move ids to them.

## 5. End the session

When the student stops, call `end_session` with the `sessionId`. Tell the student the `message` it returns, which says how many of their messages were recorded.
