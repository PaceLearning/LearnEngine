using LearnEngine.Core.Choosing;
using LearnEngine.Core.History;
using LearnEngine.Core.Learning;
using LearnEngine.Core.Registration;
using LearnEngine.Mcp;
using LearnEngine.Mcp.Prompts;
using LearnEngine.Mcp.Storage;
using LearnEngine.Mcp.Tools;
using LearnEngine.Teaching;
using LearnEngine.Web.Components;
using LearnEngine.Web.Feed;
using LearnEngine.Web.Sessions;
using LearnEngine.Web.Tools;
using ModelContextProtocol.Protocol;

var builder = WebApplication.CreateBuilder(args);

// The same data folder and learner as the stdio server, so the sessions it kept show up here too.
var dataFolder = Environment.GetEnvironmentVariable("LEARNENGINE_DATA");
if (string.IsNullOrWhiteSpace(dataFolder))
    dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LearnEngine");

var learnerId = Environment.GetEnvironmentVariable("LEARNENGINE_LEARNER");
if (string.IsNullOrWhiteSpace(learnerId))
    learnerId = "me";

builder.Services
    .AddFileStores(ServerSettings.Create(dataFolder, learnerId))
    .AddTeachingVocabulary()
    .AddLearnEngine();

builder.Services.AddSingleton(TurnGate.Create());
builder.Services.AddSingleton(EngineFeed.Create());
builder.Services.AddSingleton<LearnTools>();
builder.Services.AddSingleton(provider => SessionReader.Create(
    provider.GetRequiredService<IHistoryStore>(),
    provider.GetRequiredService<HistoryReplayer>(),
    provider.GetRequiredService<ILearnerModelStore>(),
    provider.GetRequiredService<MoveChooser>()));

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new Implementation { Name = "learnengine", Title = "LearnEngine", Version = "1.0.0" };
        options.ServerInstructions = ServerInstructions.Text;
    })
    .WithHttpTransport()
    .WithTools<WebLearnTools>(ToolResults.Options)
    .WithPrompts<LearnPrompts>(ToolResults.Options);

var app = builder.Build();

app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapMcp("/mcp");

app.Run();
