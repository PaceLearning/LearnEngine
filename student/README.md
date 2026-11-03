# Learn with LearnEngine

LearnEngine makes Claude a tutor that notices how you learn and changes how it teaches you. It runs on your own computer, and what it learns about you stays there.

## Set it up

1. Install [Docker Desktop](https://www.docker.com/products/docker-desktop/) and start it. Keep it running whenever you learn.
2. Open Claude Desktop. Go to Settings, then Developer, then Edit Config. This opens the file `claude_desktop_config.json`:
   - Windows: `%APPDATA%\Claude\claude_desktop_config.json`
   - macOS: `~/Library/Application Support/Claude/claude_desktop_config.json`
3. If the file is empty or holds only `{}`, replace it with this. You can also copy it from [claude_desktop_config.json](claude_desktop_config.json).

   ```json
   {
     "mcpServers": {
       "learnengine": {
         "command": "docker",
         "args": ["run", "-i", "--rm", "-v", "learnengine:/data", "ghcr.io/pacelearning/learnengine-mcp:latest"]
       }
     }
   }
   ```

   If it already has `mcpServers`, add only the `"learnengine": { ... }` entry inside it, with a comma after the entry before it.
4. Save the file. Quit Claude Desktop fully and open it again.
5. The first time Claude calls `report_turn`, it asks for permission. Choose **Always allow**. Otherwise Claude asks you again on every message.

## Start learning

Click the plus button in the message box, pick LearnEngine, then **Start learning**, and type what you want to learn. Or just tell Claude what you want to learn.

When you are done, say so. Claude tells you how many of your messages were recorded.

## Optional extras

LearnEngine works without these. They help Claude follow the rules more reliably.

- **The skill.** Download [learnengine-skill.zip](learnengine-skill.zip). In Claude, go to Customize, then Skills, click the plus button, choose Create skill, then Upload a skill, and pick the zip. Skills need code execution, which you turn on in Settings, then Capabilities.
- **A Project.** Create a Claude Project for your learning and paste the text from [ProjectInstructions.md](ProjectInstructions.md) into its instructions. Learn in chats inside that Project.

## Your data

Everything LearnEngine keeps lives in a Docker volume named `learnengine` on your computer. To wipe it, quit Claude Desktop and run:

```
docker volume rm learnengine
```

It starts fresh the next time you learn.
