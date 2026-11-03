# 3. The odds engine and the bandit together

The same conversation as in the first two files. Now the two parts split the work:

- **The odds engine** says what **state** Sam is in.
- **The bandit** says which **move** works for Sam **in that state**.

The old hypotheses mixed the two: `needs_example` was both a state ("stuck") and a move ("give an example"). Here they
are split.

## The states

The same five as in `1-Odds.md`, with the same priors and the same signal numbers, renamed to say what Sam is
rather than what the tutor does. Because the numbers are the same, **the odds on every turn are exactly those of the
first file**. Only what is done with them changes.

| State | Was | Prior |
|---|---|---|
| `overloaded` | overloaded | 15% |
| `model_clash` | model_clash | 10% |
| `following` | following | 35% |
| `stuck` | needs_example | 25% |
| `eager` | wants_to_do | 15% |

## The moves, per state

Each state has its own short list of moves that make sense in it, each with its own score. A new learner starts from
the scores across all learners. These start numbers are made up for this example.

| State | Moves (worked / tried) |
|---|---|
| following | **keep_going 7/10**, let_them_try 6/10, worked_example 4/10 |
| model_clash | **ask_their_picture 7/10**, worked_example 5/10, explain_again 3/10 |
| stuck | **worked_example 7/10**, diagram 6/10, explain_again 4/10 |
| overloaded | **slow_down 7/10**, worked_example 3/10, keep_going 2/10 |
| eager | **let_them_try 8/10**, keep_going 5/10 |

## How a turn runs

1. The AI reports the signals.
2. The odds engine fades, multiplies and divides, as in the first file.
3. **If the leading state is clear,** the bandit picks the best move for that state.
4. **If it is not clear,** the bandit picks nothing and nothing is learned. The tutor keeps going, and still answers
   anything Sam asks for outright.
5. On the next learner turn the move is judged as in the second file (failed, worked or no result) and **the score of
   that move in that state** is updated.

## The conversation

**Tutor:** Recursion is when a function calls itself. `factorial(n)` is `n` times `factorial(n - 1)`.

**Sam (1):** Okay.
> signals: `reply.minimal`
> state: **following, clear** (54%)
> bandit, in following: **keep_going** (7/10)

**Tutor:** What do you think `factorial(3)` gives?

**Sam (2):** 6? Because 3 times 2 times 1.
> signals: `offers.own_answer`. Last move **worked**: following / keep_going 8/11
> state: **following, clear** (75%)
> bandit, in following: **keep_going** (8/11)

**Tutor:** Right. To stop, factorial has a base case: when `n` is 1, it returns 1 without calling itself.

**Sam (3):** Wait, why does it stop at 1? I thought it would keep going down to 0, then -1.
> signals: `asks.how_or_why`, `offers.own_model`. **No result.**
> state: **following, clear** (80%)
> bandit, in following: **keep_going** (8/11)

**Tutor:** It stops because the base case returns 1, and nothing calls further.

**Sam (4):** No, I still think it should go to 0. Counting stops at 0.
> signals: `pushes.back`, `offers.own_model`. Last move **failed**: following / keep_going 8/12
> state: **model_clash, clear** (81%)
> bandit, in model_clash: **ask_their_picture** (7/10)

**Tutor:** Fair. If it did go to 0, what would `factorial(0)` have to return so `factorial(3)` is still 6?

**Sam (5):** I don't get it. Can you show me what happens step by step?
> signals: `says.dont_get_it`, `asks.for_help`. Last move **failed**: model_clash / ask_their_picture 7/11
> state: stuck 44%, model_clash 41%, **not clear**
> bandit: no pick. The tutor answers what Sam asked for: a step by step trace.

**Tutor:** `factorial(3)` is 3 times `factorial(2)`, which is 2 times `factorial(1)`, which is 1. On the way back up: 2
times 1 is 2, then 3 times 2 is 6.

**Sam (6):** Oh, so `factorial(1)` gives back 1 and then each call multiplies on the way back up?
> signals: `offers.own_answer`. Nothing to judge: the last turn had no pick.
> state: model_clash 33%, following 27%, **not clear**
> bandit: no pick

**Tutor:** Exactly.

**Sam (7):** Can I try writing `sum(n)` myself? I think it is `sum(n - 1)` plus `n`.
> signals: `offers.own_answer`
> state: **following, clear** (54%)
> bandit, in following: **keep_going** (8/12)

**Tutor:** Good. Before that, let me show you how the call stack keeps each frame, what a tail call is, and why deep
recursion overflows.

**Sam (8):** That's a lot. My head is spinning.
> signals: `says.overwhelmed`. Last move **failed**: following / keep_going 8/13
> state: overloaded 29%, following 27%, **not clear**
> bandit: no pick

**Tutor:** Let's stop there. You did the main part.

**Sam (9):** Okay.
> signals: `reply.minimal`
> state: **following, clear** (45%)

## Sam's scores after the session

| State | Move | Started | After |
|---|---|---|---|
| following | keep_going | 7 / 10 | 8 / 13 |
| model_clash | ask_their_picture | 7 / 10 | 7 / 11 |
| every other state and move | | unchanged | unchanged |

## The three side by side

| Turn | Sam | Odds only | Bandit only | Both |
|---|---|---|---|---|
| 1 | Okay | following: keep going | worked_example | following: keep_going |
| 3 | why 1? I thought 0 | following: keep going | worked_example | following: keep_going |
| 4 | No, it should go to 0 | model_clash: find the picture | worked_example | model_clash: ask_their_picture |
| 5 | show me step by step | not clear | worked_example | not clear, answer the request |
| 7 | can I try sum(n)? | following: keep going | worked_example | following: keep_going |
| 8 | my head is spinning | not clear | worked_example | not clear |

## What the two together get

- **The right kind of move for the situation.** At turn 4 the odds say model_clash and the bandit picks
  ask_their_picture, which is what the tutor did. The bandit alone recommended an example there.
- **Learning that stays in its situation.** keep_going failing for Sam lowers keep_going in following only. It does
  not make keep_going look worse when Sam is eager.
- **Restraint.** When the state is not clear, nothing is picked and nothing is learned, so the scores are not filled
  with guesses.

## What it still gets wrong

The bandit can only be as good as the state it is given, and the state comes from the old numbers:

- **Turn 3 and turn 8 are still missed.** The odds say following at turn 3 and not clear at turn 8, so the bandit
  never gets the chance to pick find the picture or slow_down. Fixing that is the odds engine's job: the likelihood
  meaning B from the plan, honest numbers, or letting a strong signal like `says.overwhelmed` count for more.
- **A failure is blamed on the wrong move when the state changes.** keep_going "failed" at turn 4, but the cause was
  Sam's different picture appearing, not that keeping going was wrong while Sam was following. A move should
  probably only be judged when the state is still the one it was picked for.
- **The signals for "worked" are thin.** The only success signal is `offers.own_answer`. The vocabulary needs at least
  `answers.correctly` and `says.it_clicked`, and turn 7 shows it also needs `asks.to_try` for the eager state.

These are the open questions for the plan before the first type is written.
