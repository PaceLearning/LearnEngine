using LearnEngine.Core.Registration;
using LearnEngine.Mcp.Storage;
using LearnEngine.Mcp.Tools;
using LearnEngine.Mcp.Vocabulary;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Stdout carries only the JSON-RPC messages of the stdio transport, so every log line goes to stderr.
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

// The Docker image sets LEARNENGINE_DATA to its /data volume. Run outside Docker, the files go to a local folder.
var dataFolder = Environment.GetEnvironmentVariable("LEARNENGINE_DATA");
if (string.IsNullOrWhiteSpace(dataFolder))
    dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LearnEngine");

// One student runs one server, so every session and score belongs to the same learner.
var learnerId = Environment.GetEnvironmentVariable("LEARNENGINE_LEARNER");
if (string.IsNullOrWhiteSpace(learnerId))
    learnerId = "me";

builder.Services
    .AddFileStores(ServerSettings.Create(dataFolder, learnerId))
    .AddTeachingVocabulary()
    .AddLearnEngine();

builder.Services.AddSingleton(TurnGate.Create());

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<LearnTools>(ToolResults.Options);

await builder.Build().RunAsync();
