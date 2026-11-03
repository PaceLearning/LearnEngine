# 2. The bandit on its own

The same conversation as in `1-Odds.md`, now with no odds and no states. A bandit picks the tutor's next move and
learns from what happens after it. The name comes from a row of slot machines ("one armed bandits"): you keep pulling
the arm that has paid most, and sometimes try another in case it pays better.

The conversation is replayed as it happened, so the tutor's turns do not change. Under each learner turn is the move
the bandit would have recommended, and what it learned.

## What the bandit has

The moves the tutor can make, each with a score: how often it worked when it was tried. A new learner starts from
the scores across all learners. These start numbers are made up for this example.

| Move | Worked / tried | Score |
|---|---|---|
| `keep_going` | 5 / 10 | 50% |
| `explain_again` | 4 / 10 | 40% |
| `worked_example` | 6 / 10 | **60%** |
| `diagram` | 5 / 10 | 50% |
| `ask_their_picture` | 5 / 10 | 50% |
| `let_them_try` | 5 / 10 | 50% |
| `slow_down` | 4 / 10 | 40% |

## How it decides and learns

- **Pick:** after every learner turn, the move with the highest score. A real bandit also tries another move now and
  then, so a move that started unlucky gets another chance. That is left out here so every step can be followed.
- **Judge:** the next learner turn says whether the move worked:
  - **failed** if it has `says.dont_get_it`, `reask.same_concept`, `says.overwhelmed` or `pushes.back`;
  - **worked** if it has `offers.own_answer` and none of those;
  - otherwise **no result**, and nothing is counted.
- **Learn:** add 1 to tried, and 1 to worked when it worked.

The signals are the same ones the AI reported in the first file. Here they are not evidence about a state, only about
whether the last move worked.

## The conversation

**Tutor:** Recursion is when a function calls itself. `factorial(n)` is `n` times `factorial(n - 1)`.

**Sam (1):** Okay.
> signals: `reply.minimal`
> bandit picks: **worked_example** (6/10)

**Tutor:** What do you think `factorial(3)` gives?

**Sam (2):** 6? Because 3 times 2 times 1.
> signals: `offers.own_answer`, so the last move **worked**: worked_example 7/11
> bandit picks: **worked_example** (7/11)

**Tutor:** Right. To stop, factorial has a base case: when `n` is 1, it returns 1 without calling itself.

**Sam (3):** Wait, why does it stop at 1? I thought it would keep going down to 0, then -1.
> signals: `asks.how_or_why`, `offers.own_model`, so **no result**
> bandit picks: **worked_example** (7/11)

**Tutor:** It stops because the base case returns 1, and nothing calls further.

**Sam (4):** No, I still think it should go to 0. Counting stops at 0.
> signals: `pushes.back`, `offers.own_model`, so the last move **failed**: worked_example 7/12
> bandit picks: **worked_example** (7/12, still the best)

**Tutor:** Fair. If it did go to 0, what would `factorial(0)` have to return so `factorial(3)` is still 6?

**Sam (5):** I don't get it. Can you show me what happens step by step?
> signals: `says.dont_get_it`, `asks.for_help`, so the last move **failed**: worked_example 7/13
> bandit picks: **worked_example** (7/13, still ahead of 50%)

**Tutor:** `factorial(3)` is 3 times `factorial(2)`, which is 2 times `factorial(1)`, which is 1. On the way back up: 2
times 1 is 2, then 3 times 2 is 6.

**Sam (6):** Oh, so `factorial(1)` gives back 1 and then each call multiplies on the way back up?
> signals: `offers.own_answer`, so the last move **worked**: worked_example 8/14
> bandit picks: **worked_example** (8/14)

**Tutor:** Exactly.

**Sam (7):** Can I try writing `sum(n)` myself? I think it is `sum(n - 1)` plus `n`.
> signals: `offers.own_answer`, so the last move **worked**: worked_example 9/15
> bandit picks: **worked_example** (9/15)

**Tutor:** Good. Before that, let me show you how the call stack keeps each frame, what a tail call is, and why deep
recursion overflows.

**Sam (8):** That's a lot. My head is spinning.
> signals: `says.overwhelmed`, so the last move **failed**: worked_example 9/16
> bandit picks: **worked_example** (9/16)

**Tutor:** Let's stop there. You did the main part.

**Sam (9):** Okay.
> signals: `reply.minimal`, so **no result**

## What Sam's scores are after the session

| Move | Started | After |
|---|---|---|
| `worked_example` | 6 / 10 | 9 / 16 |
| every other move | unchanged | unchanged |

The next session with Sam starts from these numbers, not from the all learner ones.

## What it got right

- **It learns from what happened.** Every move is judged by Sam's next turn, and the score follows.
- **It is about Sam.** Sam's scores belong to Sam. Over many sessions it would find out that, for this learner, a step
  by step trace works.
- **It needs no guessed numbers about states.** Only counts of what worked.

## What it got wrong

- **It recommends the same move every turn.** Once worked_example is ahead, nothing else gets tried. Trying another
  move now and then would help, but only slowly.
- **It cannot tell situations apart.** A worked example is right at turn 5, when Sam asks for one, and wrong at turn
  8, when Sam is overwhelmed. The bandit has one score per move for every situation, so it recommends an example to
  someone whose head is spinning.
- **Its scores mix situations.** worked_example lost points at turns 4 and 5, when the real problem was Sam's
  different picture. The move was blamed for a situation it did not fit.
- **It never notices the different picture at all.** Nothing here can say "Sam thinks it goes to 0".

The odds engine knows the situation but learns nothing. The bandit learns but cannot see the situation. The third
file runs them together.
