using IconLibraryHarvester;
using Spectre.Console;
using Spectre.Console.Cli;

if (args.Length == 0)
{
    AnsiConsole.Write(new FigletText("Icon Library Harvester").Centered().Color(Color.Blue));
    AnsiConsole.MarkupLine($"[{Color.Cyan1}]-h for help[/]");
    return 0;
}

var app = new CommandApp<HarvestCommand>();
return await app.RunAsync(args);
