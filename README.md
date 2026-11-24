# LearnEngine

LearnEngine helps an AI tutor decide what to do next. After each learner turn, the AI reports what the learner did as signals, the topic the turn was about and the move it used last. The engine keeps odds on the state the learner is in, such as stuck or following, and learns from what happens next which teaching move works for that learner in that state.

Each answer of `report_turn` gives the move to make with why it was chosen, and what the engine expects of the learner's next turn: the signals they will likely show and the topics they will likely ask about. The engine learns how the learner learns from how far each turn was from what it expected. `start_session` gives back the topic ids the learner has had before, so the AI reuses them.

## Use it with Claude Desktop

LearnEngine runs as an MCP server in Docker. You need [Docker Desktop](https://www.docker.com/products/docker-desktop/) running.

1. Build the image straight from this repository:

   ```
   docker build -t learnengine-mcp https://github.com/PaceLearning/LearnEngine.git#main
   ```

2. Add the server to Claude Desktop's config (Settings, Developer, Edit Config):

   ```json
   {
     "mcpServers": {
       "learnengine": {
         "command": "docker",
         "args": ["run", "-i", "--rm", "-v", "learnengine:/data", "learnengine-mcp"]
       }
     }
   }
   ```

3. Restart Claude Desktop and tell Claude what you want to learn.

What LearnEngine learns about you stays in the Docker volume `learnengine` on your computer. Rebuild the image with the same command to update it.

Students: the full guide, a skill to download and text to paste into a Project are in [student/README.md](student/README.md).

## Projects

- `src/LearnEngine.Core`: the engine. It keeps the odds, learns how each learner learns, chooses the move most likely to bring them to a goal state, and keeps each session as a history of events. It never stores the learner's words.
- `src/LearnEngine.Teaching`: the teaching vocabulary, all in `RegistrationExtension.cs`: the signals, the states with their priors, the rate of every signal in every state, the moves allowed in each state, the goal states and where each move leads. A host registers it with `AddTeachingVocabulary()`.
- `src/LearnEngine.Mcp`: the MCP server for Claude Desktop, with file stores for one learner.
- `tests/LearnEngine.Core.Tests`: the tests of the engine.
- `tests/LearnEngine.Mcp.Tests`: the tests of the MCP server's tools, called as the AI calls them.

LearnEngine.Core and LearnEngine.Teaching are packed as NuGet packages with `dotnet pack -c Release`, for a host such as Pace. A host that stores history events as JSON writes them with `HistoryEventJson` or with `HistoryJsonContext.Default.HistoryEvent`.
