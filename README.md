# LearnEngine

LearnEngine helps an AI tutor decide what to do next. After each learner turn, the AI reports what the learner did as signals. The engine keeps odds on the state the learner is in, such as confused or following, and learns from what happens next which teaching move works for that learner in that state.

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
