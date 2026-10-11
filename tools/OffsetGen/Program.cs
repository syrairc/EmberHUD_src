using System.Security.Cryptography;
using OffsetGen;
using Aember.Host;

if (args.Length != 2)
{
    Console.Error.WriteLine("usage: OffsetGen <layout.want> <outDir>");
    return 1;
}

var layout = File.ReadAllText(args[0]);
var entries = LayoutParser.Parse(layout);
var strings = LayoutParser.ParseStrings(layout);
var seed = RandomNumberGenerator.GetBytes(32);
Directory.CreateDirectory(args[1]);
File.WriteAllText(Path.Combine(args[1], "Offsets.g.cs"), OffsetEmitter.Emit([.. entries.Select(e => e.Name)], seed, [.. strings.Select(e => e.Name)]));
File.WriteAllBytes(Path.Combine(args[1], "offsets.bin"), OffsetTable.Seal(OffsetTable.Pack([.. entries.Select(e => e.Value)], [.. strings.Select(e => e.Value)]), seed));
Console.WriteLine($"wrote {entries.Count} offsets and {strings.Count} strings to {args[1]}");
return 0;
