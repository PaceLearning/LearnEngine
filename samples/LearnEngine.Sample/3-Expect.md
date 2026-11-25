# 3. What the engine expects next

The same run as in the first two files. After every turn the engine also says what it expects of Sam's next message,
assuming the tutor makes the picked move, or no move when none was picked. It keeps that in the history, and measures
the next turn against it. The AI can use it to be ready: for a pushback, for a request to try, for a topic Sam may come
back to.

## The signals

The engine predicts the odds of every state at the next message from the odds now and the move, as in `1-Odds.md`. The
chance of each signal is then the sum over every state of its predicted odds times Sam's rate of the signal there. Its
usual chance is the same sum over the priors, as if nothing were known about Sam. A signal whose chance is above its
usual one is likelier than usual, and those are the ones worth getting ready for.

Turn 2: Sam answers correctly, following is at 75% and the engine picks let_them_try. It expects:

```
expect, confidence low: asks.to_try 10% (1.5 times usual), offers.own_answer 19% (1.2 times usual), answers.correctly 13% (1.1 times usual)
```

Turn 4: Sam pushes back, model_clash is at 88% and the engine picks ask_their_picture. It expects more of the same:
ask_their_picture leaves a learner in model_clash 20% of the time, and for Sam it has not worked yet.

```
expect, confidence medium: pushes.back 8% (1.5 times usual), offers.own_model 14% (1.5 times usual), offers.own_answer 21% (1.3 times usual)
```

The chances are small on their own. A rate is the chance a signal shows up on any one message, and most messages show
only one or two of the fourteen signals.

## The confidence

The confidence of what is expected follows the same rule as the confidence of the state, applied to the predicted odds:
low when the predicted leader is not 15 points ahead, medium when it is, and high when it is 40 points ahead. It is
often lower than the confidence of the state, because a move spreads the odds over where it may lead.

## The topics

The AI sends a topic id with each turn, such as `recursion.base_case`. The chance of each topic next is worked out from:

- one count for every time Sam went from the last topic to it;
- 1 more for the last topic itself, since a learner often stays on a topic;
- half the share of Sam's turns on the topic that were not in a goal state, since a learner may come back to a topic
  they had trouble with.

These are divided by their total. On turn 7 Sam moves to `recursion.sum`, and the engine expects:

```
expect topics: recursion.sum 69%, recursion.base_case 23%, recursion.idea 8%
```

recursion.base_case is ahead of recursion.idea because Sam spent turns 3 to 5 on it in model_clash and stuck.

The topics outlive the session. `start_session` in the MCP server gives them back, so the AI reuses the same ids, and the
sample prints them when it starts a second session.

## The surprise

How far a turn was from what was expected of it: minus the natural log of the chance of exactly that set of signals,
each shown or not shown. It is 0 for a turn that was certain and grows the more unexpected the turn was. Turn 1, a
plain "Okay.", has 2.77. Turn 5, "I don't get it" with a request for help right after a pushback, has 7.33.

The engine learns from the miss: every turn adds the odds after it to Sam's counts of each signal in each state, as
shown or not shown, so a signal Sam shows more or less often than the shared rate says moves Sam's own rate.
