using Mediator;
using Minibox.Messy.Application.Features.Things;

// Vi phạm cố ý: endpoint viết lambda trong Program, gửi query trực tiếp.
var app = WebApplication.CreateBuilder(args).Build();
app.MapGet("/things", (ISender sender, CancellationToken ct) => sender.Send(new GetThing(), ct));
app.Run();
