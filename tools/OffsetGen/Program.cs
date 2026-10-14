using System.Security.Cryptography;
using OffsetGen;

if (args.Length != 2)
{
    Console.Error.WriteLine("usage: OffsetGen <layout.want> <outDir>");
    return 1;
}

var layout = File.ReadAllText(args[0]);
var entries = LayoutParser.Parse(layout);
var strings = LayoutParser.ParseStrings(layout);
var builds = LayoutParser.ParseBuilds(layout, entries.Select(e => e.Name));
var seed = RandomNumberGenerator.GetBytes(32);
Directory.CreateDirectory(args[1]);
File.WriteAllText(Path.Combine(args[1], "Offsets.g.cs"), OffsetEmitter.Emit([.. entries.Select(e => e.Name)], seed, [.. strings.Select(e => e.Name)], builds.Names, builds.Sizes));
File.WriteAllBytes(Path.Combine(args[1], "offsets.bin"), OffsetEmitter.Seal(OffsetEmitter.Pack([.. entries.Select(e => e.Value), .. builds.Values], [.. strings.Select(e => e.Value)]), seed));
Console.WriteLine($"wrote {entries.Count} offsets, {builds.Names.Length} rvas x {builds.Sizes.Length} builds and {strings.Count} strings to {args[1]}");
return 0;
