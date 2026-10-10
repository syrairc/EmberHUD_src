using System.Security.Cryptography;
using OffsetGen;
using TorchlightSDK.Host;

if (args.Length != 2)
{
    Console.Error.WriteLine("usage: OffsetGen <layout.want> <outDir>");
    return 1;
}

var entries = LayoutParser.Parse(File.ReadAllText(args[0]));
var seed = RandomNumberGenerator.GetBytes(32);
Directory.CreateDirectory(args[1]);
File.WriteAllText(Path.Combine(args[1], "Offsets.g.cs"), OffsetEmitter.Emit([.. entries.Select(e => e.Name)], seed));
File.WriteAllBytes(Path.Combine(args[1], "offsets.bin"), OffsetTable.Seal(OffsetTable.Pack([.. entries.Select(e => e.Value)]), seed));
Console.WriteLine($"wrote {entries.Count} offsets to {args[1]}");
return 0;
