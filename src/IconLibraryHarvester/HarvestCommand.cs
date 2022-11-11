using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using ImageTools;
using Spectre.Console;
using Spectre.Console.Cli;

namespace IconLibraryHarvester;

public sealed class HarvestCommand : AsyncCommand<HarvestCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [Description("The file path to the icon library")]
        [CommandArgument(0, "[filePath]")]
        [AllowNull]
        public string FilePath { get; init; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(FilePath) || FilePath.Length < 5)
                return ValidationResult.Error("File path must at least be 5 characters long");

            if (!IsFileExtensionValid)
                ValidationResult.Error("Must have the file extension exe or dll");

            if (!File.Exists(FilePath))
                ValidationResult.Error("File does not exist please verify that the file path is correct");
            
            return ValidationResult.Success();
        }

        private bool IsFileExtensionValid => 
            new[] {"exe", "dll"}.ToList().Contains(Path.GetExtension(FilePath));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var iconExtractor = new DllIconExtractor(settings.FilePath);
        var count = iconExtractor.Count;
        switch (count)
        {
            case > 0:
                AnsiConsole.MarkupLine($"[{Spectre.Console.Color.Green}]There is {count} icons in the file.[/]");
                break;
            case 0:
                AnsiConsole.MarkupLine($"[{Spectre.Console.Color.Red}]Found no icons in the file. Aborting extraction.[/]");
                return -1;
        }

        var directoryName = Path.GetFileNameWithoutExtension(settings.FilePath);
        if(!Directory.Exists(directoryName))
            Directory.CreateDirectory(directoryName);

        var appPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, directoryName);

        Icon[]? icons = null;
        AnsiConsole.Status().Start("Extracting icons..", _ => icons = iconExtractor.GetAllIcons());

        AnsiConsole.MarkupLine($"[{Spectre.Console.Color.Green}]{icons.Length } icons extracted.[/]");

        for (var i = 0; i < count; i++)
        {
            var iconFilePath = Path.Combine(appPath, $"icon{i}.ico");
            await using var fileStream = new FileStream(iconFilePath, FileMode.Create);
            icons[i].Save(fileStream);
        }

        return 0;
    }
}
