# 2. The move to make

The same run as in `1-Odds.md`. When the leading state is clear, the engine picks the move most likely to bring Sam to
a goal state by the next message, and says why. When it is not clear, it picks nothing: the tutor teaches as usual and
still answers anything Sam asks for outright.

## What the engine has

The goal states are `following` and `eager`. Each state allows a few moves:

| State | Allowed moves |
|---|---|
| following | keep_going, let_them_try, worked_example |
| model_clash | ask_their_picture, worked_example, explain_again |
| stuck | worked_example, diagram, explain_again |
| overloaded | slow_down, worked_example, keep_going |
| eager | let_them_try, keep_going |

Each allowed move has an effect: where it leads from its state by the next message, shared by every learner. For
example, from following:

| Move | following | eager | stuck | overloaded | model_clash | to a goal |
|---|---|---|---|---|---|---|
| keep_going | 0.55 | 0.15 | 0.15 | 0.10 | 0.05 | 70% |
| let_them_try | 0.35 | 0.25 | 0.25 | 0.05 | 0.10 | 60% |
| worked_example | 0.30 | 0.10 | 0.20 | 0.30 | 0.10 | 40% |

Each effect counts as 10 messages of evidence before a learner's own counts move it. Like the rates, the effects are
guesses written before any session was recorded.

## How a move is picked

For each move allowed in the clear state:

- **Goal chance:** for every state, its odds times the chance the move, made there, leads to a goal. The odds of the
  other states count too, since the engine is never sure. A move with no effect in a state is read there as no move,
  the fade.
- **Bonus:** the square root of 2 times the natural log of N, divided by n. Here n is Sam's own count of turns after the
  move in the state plus the weight of its effect, and N is the sum of n over every allowed move. A move Sam has seen
  less gets a larger bonus, so it still gets a chance to show it works better for Sam.

The move with the highest goal chance plus bonus is picked. Turn 1, with following at 51%:

| Move | Goal chance | Bonus | Total |
|---|---|---|---|
| keep_going | 45.8% | 0.26 | **0.72** |
| worked_example | 45.6% | 0.26 | 0.72 |
| let_them_try | 39.8% | 0.26 | 0.66 |

Sam has seen no move yet, so every n is 10, N is 30 and the bonus is the same for each. keep_going is ahead by a
fraction of a point. Its goal chance is below the 70% of the table because the other states count too: keep_going
made while Sam is overloaded leads to a goal only 20% of the time.

The sample prints:

```
move: keep_going, goal chance 46%. Go on to the next small step at the same pace, and check in with one short question.
why: This learner has not had keep_going in following yet, so the shared guess is that it leads to following or eager 70% of the time.
```

## How it learns which moves work for Sam

The AI reports the move it made with the next turn. The engine then counts where Sam went after it: each turn adds one
step in all, split over the state before and the state after by their odds, so the counts are soft. Sam's chance that
a move leads to a state is

    (shared chance times weight + Sam's steps there) / (weight + all of Sam's steps after the move in that state)

Sam's rate of every signal in every state moves the same way, with the shared rate counting as 20 messages.

Turn 3 picks ask_their_picture in model_clash. On turn 4 Sam pushes back, still in model_clash, so that step goes
against it. The why says so:

```
why: For this learner, ask_their_picture led from model_clash to following or eager 0 of 1 times.
```

It is picked again anyway: about one step against 10 messages of a shared 70% leaves it the best move in model_clash. On
turn 5 Sam is stuck and worked_example is picked, and turn 6 shows it led to following.

| Turn | State | Move picked |
|---|---|---|
| 1 | following | keep_going |
| 2 | following | let_them_try |
| 3 | model_clash | ask_their_picture |
| 4 | model_clash | ask_their_picture |
| 5 | stuck | worked_example |
| 6 | following | keep_going |
| 7 | eager | let_them_try |
| 8 | not clear | none |
| 9 | overloaded | slow_down |

Sam's learner model outlives the session. The sample ends by starting a second session and reading it: the topics Sam
has had, and Sam's own steps after ask_their_picture in model_clash.

`3-Expect.md` shows what the engine expects of the next message.
