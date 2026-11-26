using LearnEngine.Core.Choosing;
using LearnEngine.Core.Learning;
using LearnEngine.Core.Odds;
using LearnEngine.Core.Registration;
using LearnEngine.Core.Sessions;
using LearnEngine.Teaching;
using Microsoft.Extensions.DependencyInjection;

// Sam learns recursion from an AI tutor. The conversation is invented. Each of Sam's messages is reported as the AI
// reports it: the ids of the signals it shows, never Sam's words, and the topic it is about. The move made is the one
// the engine picked after the turn before, since the tutor makes the move it is given.
(string Says, string[] Signals, string Topic)[] turns =
[
    ("Okay.", ["reply.minimal"], "recursion.idea"),
    ("6? Because 3 times 2 times 1.", ["answers.correctly", "offers.own_answer"], "recursion.idea"),
    ("Wait, why does it stop at 1? I thought it would keep going down to 0, then -1.", ["asks.how_or_why", "offers.own_model"], "recursion.base_case"),
    ("No, I still think it should go to 0. Counting stops at 0.", ["pushes.back", "offers.own_model"], "recursion.base_case"),
    ("I don't get it. Can you show me what happens step by step?", ["says.dont_get_it", "asks.for_help"], "recursion.base_case"),
    ("Oh, so factorial(1) gives back 1 and each call multiplies on the way back up?", ["says.it_clicked", "offers.own_model"], "recursion.base_case"),
    ("Can I try writing sum(n) myself? I think it is sum(n - 1) plus n.", ["asks.to_try", "offers.own_answer"], "recursion.sum"),
    ("That's a lot. My head is spinning.", ["says.overwhelmed"], "recursion.call_stack"),
    ("Okay.", ["reply.minimal"], "recursion.call_stack"),
];

// The teaching vocabulary LearnEngine ships with, kept in memory. A host that keeps sessions registers its own
// IHistoryStore and ILearnerModelStore before AddTeachingVocabulary.
var services = new ServiceCollection()
    .AddTeachingVocabulary()
    .AddLearnEngine();
await using var provider = services.BuildServiceProvider();
var engine = provider.GetRequiredService<SessionEngine>();
var models = provider.GetRequiredService<ILearnerModelStore>();

const string learner = "sam";
var started = await engine.StartSession(learner, "first", CancellationToken.None);
if (started is not SessionBelief opened)
    return;

var priors = string.Join(", ", opened.All.Select(stateOdds => $"{stateOdds.StateId} {Math.Round(stateOdds.Odds * 100)}%"));
Console.WriteLine($"Session started. The odds before Sam says anything: {priors}.");
Console.WriteLine();

string? moveMade = null;
for (var index = 0; index < turns.Length; index++)
{
    var (says, signals, topic) = turns[index];
    var reported = await engine.ReportTurn(learner, "first", signals, topic, moveMade, CancellationToken.None);
    // Every id above is in the vocabulary and the session is started, so the turn is always kept.
    if (reported is not TurnResult result)
        return;

    var belief = result.Belief;
    Console.WriteLine($"Sam ({index + 1}): {says}");
    Console.WriteLine($"  reported: {string.Join(", ", signals)} on {topic}, after {moveMade ?? "no move"}");

    var state = $"not clear: {belief.Leader.StateId} {Math.Round(belief.Leader.Odds * 100)}% or {belief.RunnerUp?.StateId} {Math.Round((belief.RunnerUp?.Odds ?? 0) * 100)}%";
    if (belief.IsClear)
        state = $"{belief.Leader.StateId} {Math.Round(belief.Leader.Odds * 100)}%, clear by {Math.Round(belief.Margin * 100)} points";
    Console.WriteLine($"  state: {state}, confidence {belief.Confidence.ToString().ToLowerInvariant()}");

    if (result.Pick is PickedMove picked)
    {
        Console.WriteLine($"  move: {picked.Move.Id}, goal chance {Math.Round(picked.GoalChance * 100)}%. {picked.Move.Description}");
        Console.WriteLine($"  why: {picked.Why}");
    }

    if (result.Pick is NoClearLeader)
        Console.WriteLine("  move: none, the state is not clear. Teach as usual and answer what Sam asks for.");

    if (result.Pick is NoMovesInState noMoves)
        Console.WriteLine($"  move: none, no move is allowed in {noMoves.StateId}.");

    // The signals likelier than usual and the topics are what the AI can get ready for. The confidence uses the same
    // clear margin as the state's.
    var expectConfidence = result.Expected.Confidence(belief.ClearMargin).ToString().ToLowerInvariant();
    var expectSignals = string.Join(", ", result.Expected.AboveUsual.Take(3).Select(signal => $"{signal.SignalId} {Math.Round(signal.Chance * 100)}% ({signal.VsUsual:0.0} times usual)"));
    var expectTopics = string.Join(", ", result.Expected.Topics.Take(3).Select(topicChance => $"{topicChance.Topic} {Math.Round(topicChance.Chance * 100)}%"));
    Console.WriteLine($"  expect, confidence {expectConfidence}: {expectSignals}");
    Console.WriteLine($"  expect topics: {expectTopics}");
    Console.WriteLine($"  this turn's surprise: {result.Surprise:0.00}");
    Console.WriteLine();

    moveMade = result.Pick is PickedMove next ? next.Move.Id : null;
}

// The learner model outlives the session. The next session starts with the topics Sam has had, so the AI reuses their
// ids, and with Sam's own rates and move effects.
await engine.StartSession(learner, "second", CancellationToken.None);
var model = await models.Load(learner, CancellationToken.None);
Console.WriteLine($"Second session started. Topics Sam has had: {string.Join(", ", model.Topics)}.");

var steps = model.Steps("model_clash", "ask_their_picture");
Console.WriteLine($"Sam's own turns after ask_their_picture in model_clash: {steps:0.0}.");
