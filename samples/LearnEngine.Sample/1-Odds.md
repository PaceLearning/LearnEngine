# 1. The odds

Run the sample from the root of the repository:

```
dotnet run --project samples/LearnEngine.Sample
```

It replays one invented conversation through the engine with the teaching vocabulary LearnEngine ships with. Sam is
learning recursion from an AI tutor. For each of Sam's messages the AI reports the ids of the signals it shows, never
Sam's words, the topic it is about and the move the tutor made in the reply before it. This file is about the first
thing the engine does with a turn: it keeps odds on the state Sam is in.

## What the engine has

Five states, each with a prior: its odds before anything is seen. They are in
`src/LearnEngine.Teaching/RegistrationExtension.cs`.

| State | Prior | Meaning |
|---|---|---|
| `overloaded` | 15% | Too much is new at once |
| `model_clash` | 10% | Sam holds a different picture of the idea than the one taught |
| `following` | 35% | The explanations are landing |
| `stuck` | 25% | Sam needs something concrete, such as a worked case |
| `eager` | 15% | Sam wants to try it rather than hear more |

Fourteen signals, each with a rate in every state: the chance the signal shows up on any one message while the learner
is in that state. A few of them:

| Signal | overloaded | model_clash | following | stuck | eager |
|---|---|---|---|---|---|
| `reply.minimal` | 0.25 | 0.08 | 0.30 | 0.12 | 0.10 |
| `says.dont_get_it` | 0.15 | 0.08 | 0.01 | 0.20 | 0.02 |
| `pushes.back` | 0.03 | 0.25 | 0.02 | 0.04 | 0.04 |
| `says.overwhelmed` | 0.12 | 0.02 | 0.01 | 0.04 | 0.01 |
| `asks.to_try` | 0.01 | 0.02 | 0.06 | 0.02 | 0.25 |

The rates and priors are guesses, written before any session was recorded.

## How one turn is worked out

A turn moves the odds in two steps.

1. **Predict** where the move made leads. Each move has an effect in each state it is allowed in: the chance of each
   state by the next message. With no move made, every state keeps 85% of its odds and gets 15% of its prior back.
   That 15% is the fade.
2. **Apply** the turn's signals. Every signal counts, not only the ones reported: a signal that showed up multiplies
   the odds of each state by its rate there, and a signal that did not show up multiplies them by 1 minus its rate.
   Then the odds are divided by their total so they add up to 100% again.

Turn 1: Sam says "Okay." and the AI reports `reply.minimal`. No move was made yet, and the odds are still the priors,
so the fade leaves them as they are.

| | overloaded | model_clash | following | stuck | eager |
|---|---|---|---|---|---|
| prior | 0.15 | 0.10 | 0.35 | 0.25 | 0.15 |
| times the rate of `reply.minimal` | 0.25 | 0.08 | 0.30 | 0.12 | 0.10 |
| times 1 minus the rate of the 13 other signals | 0.431 | 0.231 | 0.311 | 0.281 | 0.317 |
| product | 0.0162 | 0.0019 | 0.0327 | 0.0084 | 0.0048 |
| divided by the total, 0.0639 | 25% | 3% | **51%** | 13% | 7% |

This is the first line the sample prints for Sam: `following 51%, clear by 26 points`.

## Clear or not

The leader is **clear** when it is at least 15 points ahead of the runner up. Only a clear leader lets a move be picked.
The confidence the engine gives is low when the leader is not clear, medium when it is, and high when it is clear by
40 points or more.

| Turn | Sam | State |
|---|---|---|
| 1 | Okay. | following 51%, clear by 26, medium |
| 2 | 6? Because 3 times 2 times 1. | following 75%, clear by 54, high |
| 3 | Why does it stop at 1? I thought 0, then -1. | model_clash 49%, clear by 20, medium |
| 4 | No, I still think it should go to 0. | model_clash 88%, clear by 83, high |
| 5 | I don't get it. Show me step by step? | stuck 70%, clear by 54, high |
| 6 | Oh, so factorial(1) gives back 1 and each call multiplies? | following 64%, clear by 45, high |
| 7 | Can I try writing sum(n) myself? | eager 66%, clear by 35, medium |
| 8 | That's a lot. My head is spinning. | not clear: overloaded 39% or stuck 26%, low |
| 9 | Okay. | overloaded 59%, clear by 39, medium |

Turn 3 already reads Sam's own picture of the idea as a clash, and turn 4 makes it near certain. Turn 8 puts overloaded
ahead but not by 15 points, so no move is picked there. Turn 9 settles it: `reply.minimal` is twice as
likely from someone overloaded as from someone stuck, the two states ahead.

`2-Moves.md` shows what the engine does with a clear state.
