using LearnEngine.Core.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace LearnEngine.Teaching;

/// <summary>
/// Registers the teaching vocabulary LearnEngine ships with, so a host works out of the box: the signals the AI reports,
/// the states the engine keeps odds on, the moves it chooses from, the goal states and where each move leads.
/// The whole vocabulary is in this one file, so another vocabulary replaces this file and nothing else.
/// </summary>
public static class RegistrationExtension
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds the whole teaching vocabulary in the order the engine asks for: the signals, the states with their
        /// priors, the rate of every signal in every state, the odds with the default fade and clear margin, the
        /// history, the learning with the default rate weight, the moves allowed in each state, the goal states and
        /// where each allowed move leads.
        /// The rates and the move effects are guesses, written before any session was recorded, and should be replaced
        /// by numbers measured from real histories.
        /// Register the history and learner model stores before this call so they are kept instead of the in-memory ones.
        /// Changing any id, description, prior, rate, allowed move, goal state or move effect changes the vocabulary
        /// fingerprint, so sessions started before the change can no longer be continued.
        /// </summary>
        public IServiceCollection AddTeachingVocabulary()
        {
            services
                .AddSignal("reply.minimal", "The message is a short acknowledgement with nothing of its own, such as \"ok\", \"sure\" or \"got it\".")
                .AddSignal("says.dont_get_it", "The student says they do not understand, such as \"I don't get it\" or \"I'm lost\".")
                .AddSignal("asks.how_or_why", "The student asks how something works or why it is so.")
                .AddSignal("asks.for_help", "The student asks you to help, show or do something for them, such as \"can you show me?\".")
                .AddSignal("offers.own_answer", "The student gives an answer or a next step of their own, right or wrong.")
                .AddSignal("offers.own_model", "The student describes in their own words how they picture the idea working.")
                .AddSignal("pushes.back", "The student disagrees with what you said or holds on to their own view.")
                .AddSignal("reask.same_concept", "The student asks again about something already explained in this session.")
                .AddSignal("says.overwhelmed", "The student says it is too much, too fast or too confusing at once.")
                .AddSignal("asks.change_of_format", "The student asks to see it another way, such as a picture, an example, code or simpler words.")
                .AddSignal("goes.off_topic", "The message is about something other than what is being learned.")
                .AddSignal("answers.correctly", "The student answers a question you asked, and the answer is right.")
                .AddSignal("says.it_clicked", "The student says it makes sense now, such as \"oh, I see\" or \"that clicked\".")
                .AddSignal("asks.to_try", "The student asks to try it or do it themselves.");

            services.AddStates(states => states
                .AddState("overloaded", "Too much is new at once, so the student is losing track of what was said.", 0.15)
                .AddState("model_clash", "The student holds a different picture of the idea than the one taught, and reads every explanation through it.", 0.10)
                .AddState("following", "The explanations are landing and the student keeps up.", 0.35)
                .AddState("stuck", "The student cannot go on from what was said and needs something concrete, such as a worked case.", 0.25)
                .AddState("eager", "The student wants to try it themselves rather than hear more.", 0.15));

            // Each rate is the chance the signal shows up on any one student message while the student is in that
            // state, so most are small: a student who is following still says "ok" on only about a third of their
            // messages. They are guesses, written before any session was recorded, and should be replaced by rates
            // measured from real histories.
            services.AddLikelihoods(rates => rates
                .AddRates("reply.minimal", ("overloaded", 0.25), ("model_clash", 0.08), ("following", 0.30), ("stuck", 0.12), ("eager", 0.10))
                .AddRates("says.dont_get_it", ("overloaded", 0.15), ("model_clash", 0.08), ("following", 0.01), ("stuck", 0.20), ("eager", 0.02))
                .AddRates("asks.how_or_why", ("overloaded", 0.05), ("model_clash", 0.20), ("following", 0.20), ("stuck", 0.15), ("eager", 0.10))
                .AddRates("asks.for_help", ("overloaded", 0.10), ("model_clash", 0.05), ("following", 0.04), ("stuck", 0.25), ("eager", 0.05))
                .AddRates("offers.own_answer", ("overloaded", 0.03), ("model_clash", 0.15), ("following", 0.25), ("stuck", 0.05), ("eager", 0.25))
                .AddRates("offers.own_model", ("overloaded", 0.02), ("model_clash", 0.30), ("following", 0.08), ("stuck", 0.05), ("eager", 0.06))
                .AddRates("pushes.back", ("overloaded", 0.03), ("model_clash", 0.25), ("following", 0.02), ("stuck", 0.04), ("eager", 0.04))
                .AddRates("reask.same_concept", ("overloaded", 0.12), ("model_clash", 0.10), ("following", 0.03), ("stuck", 0.15), ("eager", 0.02))
                .AddRates("says.overwhelmed", ("overloaded", 0.12), ("model_clash", 0.02), ("following", 0.01), ("stuck", 0.04), ("eager", 0.01))
                .AddRates("asks.change_of_format", ("overloaded", 0.08), ("model_clash", 0.04), ("following", 0.03), ("stuck", 0.12), ("eager", 0.02))
                .AddRates("goes.off_topic", ("overloaded", 0.06), ("model_clash", 0.02), ("following", 0.04), ("stuck", 0.03), ("eager", 0.03))
                .AddRates("answers.correctly", ("overloaded", 0.02), ("model_clash", 0.05), ("following", 0.20), ("stuck", 0.03), ("eager", 0.15))
                .AddRates("says.it_clicked", ("overloaded", 0.01), ("model_clash", 0.04), ("following", 0.10), ("stuck", 0.04), ("eager", 0.05))
                .AddRates("asks.to_try", ("overloaded", 0.01), ("model_clash", 0.02), ("following", 0.06), ("stuck", 0.02), ("eager", 0.25)));

            // The shared rates count as 20 messages of evidence before a student's own counts move them. A guess too.
            services
                .AddOdds()
                .AddHistory()
                .AddLearning();

            services
                .AddMove("keep_going", "Go on to the next small step at the same pace, and check in with one short question.")
                .AddMove("let_them_try", "Give the student a small task to do themselves now, and wait for their attempt before you explain more.")
                .AddMove("worked_example", "Walk through one concrete example step by step, then ask the student to do the next step.")
                .AddMove("ask_their_picture", "Ask the student how they picture it working, then build from their picture to the right one.")
                .AddMove("explain_again", "Explain the same idea again in other words, shorter, from one new angle.")
                .AddMove("diagram", "Show the idea as a simple picture, such as a text sketch or a table, and walk through it.")
                .AddMove("slow_down", "Stop adding new ideas. Sum up the one thing that matters in two sentences and ask whether that part is clear.");

            // The moves the engine may choose from in each state.
            services
                .AddMoveFor("following", "keep_going")
                .AddMoveFor("following", "let_them_try")
                .AddMoveFor("following", "worked_example")
                .AddMoveFor("model_clash", "ask_their_picture")
                .AddMoveFor("model_clash", "worked_example")
                .AddMoveFor("model_clash", "explain_again")
                .AddMoveFor("stuck", "worked_example")
                .AddMoveFor("stuck", "diagram")
                .AddMoveFor("stuck", "explain_again")
                .AddMoveFor("overloaded", "slow_down")
                .AddMoveFor("overloaded", "worked_example")
                .AddMoveFor("overloaded", "keep_going")
                .AddMoveFor("eager", "let_them_try")
                .AddMoveFor("eager", "keep_going");

            // The states the engine wants to bring every student to.
            services
                .AddGoalState("following")
                .AddGoalState("eager");

            // Where each allowed move leads from its state by the student's next message. They are guesses too, written
            // before any session was recorded: the chance of reaching following or eager is about the share of tries
            // that worked in the starting counts the engine used before it chose moves by where they lead, and the rest
            // is spread over the other states. Each counts as 10 messages before a student's own counts move it.
            return services
                .AddMoveEffect("following", "keep_going", weight: 10,
                    ("following", 0.55), ("eager", 0.15), ("stuck", 0.15), ("overloaded", 0.10), ("model_clash", 0.05))
                .AddMoveEffect("following", "let_them_try", weight: 10,
                    ("following", 0.35), ("eager", 0.25), ("stuck", 0.25), ("overloaded", 0.05), ("model_clash", 0.10))
                .AddMoveEffect("following", "worked_example", weight: 10,
                    ("following", 0.30), ("eager", 0.10), ("stuck", 0.20), ("overloaded", 0.30), ("model_clash", 0.10))
                .AddMoveEffect("model_clash", "ask_their_picture", weight: 10,
                    ("following", 0.55), ("eager", 0.15), ("stuck", 0.05), ("overloaded", 0.05), ("model_clash", 0.20))
                .AddMoveEffect("model_clash", "worked_example", weight: 10,
                    ("following", 0.40), ("eager", 0.10), ("stuck", 0.15), ("overloaded", 0.05), ("model_clash", 0.30))
                .AddMoveEffect("model_clash", "explain_again", weight: 10,
                    ("following", 0.25), ("eager", 0.05), ("stuck", 0.10), ("overloaded", 0.10), ("model_clash", 0.50))
                .AddMoveEffect("stuck", "worked_example", weight: 10,
                    ("following", 0.55), ("eager", 0.15), ("stuck", 0.20), ("overloaded", 0.05), ("model_clash", 0.05))
                .AddMoveEffect("stuck", "diagram", weight: 10,
                    ("following", 0.50), ("eager", 0.10), ("stuck", 0.25), ("overloaded", 0.05), ("model_clash", 0.10))
                .AddMoveEffect("stuck", "explain_again", weight: 10,
                    ("following", 0.35), ("eager", 0.05), ("stuck", 0.40), ("overloaded", 0.15), ("model_clash", 0.05))
                .AddMoveEffect("overloaded", "slow_down", weight: 10,
                    ("following", 0.60), ("eager", 0.10), ("stuck", 0.10), ("overloaded", 0.15), ("model_clash", 0.05))
                .AddMoveEffect("overloaded", "worked_example", weight: 10,
                    ("following", 0.25), ("eager", 0.05), ("stuck", 0.25), ("overloaded", 0.40), ("model_clash", 0.05))
                .AddMoveEffect("overloaded", "keep_going", weight: 10,
                    ("following", 0.15), ("eager", 0.05), ("stuck", 0.15), ("overloaded", 0.60), ("model_clash", 0.05))
                .AddMoveEffect("eager", "let_them_try", weight: 10,
                    ("following", 0.35), ("eager", 0.45), ("stuck", 0.10), ("overloaded", 0.05), ("model_clash", 0.05))
                .AddMoveEffect("eager", "keep_going", weight: 10,
                    ("following", 0.35), ("eager", 0.15), ("stuck", 0.15), ("overloaded", 0.25), ("model_clash", 0.10));
        }
    }
}
