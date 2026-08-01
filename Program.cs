// See https://aka.ms/new-console-template for more information

using Microsoft.Extensions.DependencyInjection;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using SkillScanner.Mapping;
using SkillScanner.Registrations;
using SkillScanner;
using SkillScanner.Parser;
using Markdig.Parsers;
using SkillScanner.SkillRule;
using SkillScanner.Output;
using System.CommandLine;

// Create a service collection and register the dependencies


var services = new ServiceCollection();

services.AddSkillScanner();

var serviceProvider = services.BuildServiceProvider();


//Create a root command, sub command and pass the argument


var rootCommand = new RootCommand("skilllint");

var scanCommand = new Command("scan", "Scan a skill file");

scanCommand.Arguments.Add(new Argument<string>("filePath"));

scanCommand.SetAction(parseResult =>
{
    var filePath = parseResult.GetValue<string>("filePath");

    Console.WriteLine($"Scanning {filePath}");

    var scanner= serviceProvider.GetRequiredService<Scanner>();

    if (scanner == null)
   {
    Console.WriteLine("Failed to parse the skill document.");
    return;
   }
   scanner.Scan(filePath);
});

rootCommand.Subcommands.Add(scanCommand);

var result = rootCommand.Parse(args);
return result.Invoke();







