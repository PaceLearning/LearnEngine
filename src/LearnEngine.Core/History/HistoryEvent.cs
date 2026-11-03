using System.Text.Json.Serialization;

namespace LearnEngine.Core.History;

/// <summary>
/// One thing that happened in a learner's session, kept in the order it happened.
/// A session's history only ever grows, and the belief about the learner is rebuilt by replaying it from the priors.
/// No event ever holds the learner's words.
/// </summary>
// The discriminators are written in every stored line, so they never change when a class is renamed.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "event")]
[JsonDerivedType(typeof(SessionStarted), "session_started")]
[JsonDerivedType(typeof(LearnerTurn), "learner_turn")]
[JsonDerivedType(typeof(MovePicked), "move_picked")]
[JsonDerivedType(typeof(MoveJudged), "move_judged")]
public abstract class HistoryEvent
{
    // Private protected so every kind of event is one the engine knows how to replay and write.
    private protected HistoryEvent() { }
}
