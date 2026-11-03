# 1. The odds engine on its own

One conversation, replayed through the old engine: the AI reports signals for each learner turn, and the engine keeps
odds on what the tutor should do next. The conversation is the same in all three files. Only what happens inside
changes.

The conversation is invented. Sam is learning recursion from an AI tutor.

## What the engine has

Five hypotheses, each with a prior (its odds before anything is seen):

| Hypothesis | Prior | Meaning |
|---|---|---|
| `overloaded` | 15% | Too much new at once: slow down, shrink the step, or stop |
| `model_clash` | 10% | Holds a different picture from the one taught: find it and bridge to it |
| `following` | 35% | It is landing: keep going |
| `needs_example` | 25% | Needs something concrete, like a worked case |
| `wants_to_do` | 15% | Wants to try it themselves |

For each signal, how likely it is under each hypothesis (only the signals this conversation uses):

| Signal | overloaded | model_clash | following | needs_example | wants_to_do |
|---|---|---|---|---|---|
| `reply.minimal` | 0.40 | 0.15 | 0.60 | 0.25 | 0.30 |
| `asks.how_or_why` | 0.15 | 0.40 | 0.50 | 0.35 | 0.15 |
| `asks.for_help` | 0.40 | 0.10 | 0.15 | 0.50 | 0.30 |
| `says.dont_get_it` | 0.50 | 0.30 | 0.05 | 0.60 | 0.05 |
| `offers.own_answer` | 0.05 | 0.15 | 0.60 | 0.10 | 0.60 |
| `offers.own_model` | 0.10 | 0.70 | 0.20 | 0.20 | 0.10 |
| `pushes.back` | 0.20 | 0.70 | 0.05 | 0.15 | 0.10 |
| `says.overwhelmed` | 0.90 | 0.15 | 0.05 | 0.10 | 0.05 |

Before each learner turn the belief fades 15% back toward the priors. A leader is **clear** when it is at least 15
points ahead of the runner up. The tutor acts on a clear leader and otherwise keeps doing what it was doing.

## How one turn is worked out

Turn 1, Sam says "Okay." and the AI reports `reply.minimal`. Multiply each prior by the signal's likelihood, then
divide by the total so the odds add up to 100% again:

| | overloaded | model_clash | following | needs_example | wants_to_do | total |
|---|---|---|---|---|---|---|
| prior | 0.15 | 0.10 | 0.35 | 0.25 | 0.15 | 1 |
| times likelihood | 0.060 | 0.015 | 0.210 | 0.063 | 0.045 | 0.393 |
| divided by total | 15% | 4% | **54%** | 16% | 11% | 100% |

Every later turn is the same: fade, then multiply by each signal, then divide by the total.

## The conversation

**Tutor:** Recursion is when a function calls itself. `factorial(n)` is `n` times `factorial(n - 1)`.

**Sam (1):** Okay.
> signals: `reply.minimal`
> odds: following 54, needs_example 16, overloaded 15, wants_to_do 11, model_clash 4
> engine: **following, clear** (by 38). Keep going.

**Tutor:** What do you think `factorial(3)` gives?

**Sam (2):** 6? Because 3 times 2 times 1.
> signals: `offers.own_answer`
> odds: following 75, wants_to_do 18, needs_example 4, overloaded 2, model_clash 2
> engine: **following, clear** (by 57). Keep going.

**Tutor:** Right. To stop, factorial has a base case: when `n` is 1, it returns 1 without calling itself.

**Sam (3):** Wait, why does it stop at 1? I thought it would keep going down to 0, then -1.
> signals: `asks.how_or_why`, `offers.own_model`
> odds: following 80, model_clash 10, needs_example 6, wants_to_do 3, overloaded 1
> engine: **following, clear** (by 71). Keep going.

**Tutor:** It stops because the base case returns 1, and nothing calls further.

**Sam (4):** No, I still think it should go to 0. Counting stops at 0.
> signals: `pushes.back`, `offers.own_model`
> odds: model_clash 81, following 12, needs_example 4, overloaded 1, wants_to_do 1
> engine: **model_clash, clear** (by 69). Find Sam's picture and bridge to it.

**Tutor:** Fair. If it did go to 0, what would `factorial(0)` have to return so `factorial(3)` is still 6?

**Sam (5):** I don't get it. Can you show me what happens step by step?
> signals: `says.dont_get_it`, `asks.for_help`
> odds: needs_example 44, model_clash 41, overloaded 12, following 2, wants_to_do 1
> engine: needs_example ahead by 3, **not clear**. Keep doing what you were doing.

**Tutor:** `factorial(3)` is 3 times `factorial(2)`, which is 2 times `factorial(1)`, which is 1. On the way back up: 2
times 1 is 2, then 3 times 2 is 6.

**Sam (6):** Oh, so `factorial(1)` gives back 1 and then each call multiplies on the way back up?
> signals: `offers.own_answer`
> odds: model_clash 33, following 27, needs_example 25, wants_to_do 11, overloaded 4
> engine: model_clash ahead by 7, **not clear**.

**Tutor:** Exactly.

**Sam (7):** Can I try writing `sum(n)` myself? I think it is `sum(n - 1)` plus `n`.
> signals: `offers.own_answer`
> odds: following 54, wants_to_do 22, model_clash 15, needs_example 8, overloaded 1
> engine: **following, clear** (by 32). Keep going.

**Tutor:** Good. Before that, let me show you how the call stack keeps each frame, what a tail call is, and why deep
recursion overflows.

**Sam (8):** That's a lot. My head is spinning.
> signals: `says.overwhelmed`
> odds: overloaded 29, following 27, model_clash 22, needs_example 11, wants_to_do 11
> engine: overloaded ahead by 2, **not clear**. Keep doing what you were doing.

**Tutor:** Let's stop there. You did the main part.

**Sam (9):** Okay.
> signals: `reply.minimal`
> odds: following 45, overloaded 28, needs_example 9, wants_to_do 9, model_clash 8
> engine: **following, clear** (by 17).

## What it got right

- Turns 1 and 2: following, which is right.
- Turn 4: once Sam pushes back, model_clash jumps from 10% to 81% and the engine says to find Sam's picture. That is
  the move a good tutor makes.

## What it got wrong

- **Turn 3 is a miss.** Sam says outright that they picture it going to 0 and below. That is a different picture,
  but two good turns before keep following at 80%. One turn of evidence cannot beat it.
- **Turn 5 is not clear,** even though Sam asks plainly for a worked case.
- **Turn 8 is the worst miss.** Sam says their head is spinning, and the engine says not clear, keep going. Seven
  turns of evidence outweigh the strongest signal in the vocabulary.
- **It learns nothing about Sam.** The numbers are the same for every learner and every session. If step by step
  traces always work for Sam, the engine never finds out.
- **It never checks whether acting on it helped.** At turn 4 it said to find Sam's picture. Whether that worked, it
  does not know.

The second file keeps the same conversation and replaces all of this with a bandit.
