using SoulsFormats;
using System;
using System.Collections.Generic;
using System.IO;

namespace StudioCore.Tests;

public static class MSBReadWrite
{
    public static bool RunER(AssetLocator locator)
    {
        List<string> msbs = locator.GetFullMapList();
        foreach (var msb in msbs)
        {
            AssetDescription path = locator.GetMapMSB(msb);
            var bytes = File.ReadAllBytes(path.AssetPath);
            Memory<byte> decompressed = DCX.Decompress(bytes);
            MSBE m = MSBE.Read(decompressed);
            var written = m.Write(DCX.Type.None);
            if (!decompressed.Span.SequenceEqual(written))
            {
                var basepath = Path.GetDirectoryName(path.AssetPath);
                if (!Directory.Exists($@"{basepath}\mismatches"))
                {
                    Directory.CreateDirectory($@"{basepath}\mismatches");
                }

                Console.WriteLine($@"Mismatch: {msb}");
                File.WriteAllBytes($@"{basepath}\mismatches\{Path.GetFileNameWithoutExtension(path.AssetPath)}",
                    written);
            }
        }

        return true;
    }

    public static bool RunACVD(AssetLocator locator)
    {
        List<string> msbs = locator.GetFullMapList();
        foreach (var msb in msbs)
        {
            AssetDescription path = locator.GetMapMSB(msb);
            var bytes = File.ReadAllBytes(path.AssetPath);
            MSBVD m = MSBVD.Read(bytes);
            var written = m.Write(DCX.Type.None);
            if (!bytes.AsMemory().Span.SequenceEqual(written))
            {
                var basepath = Path.GetDirectoryName(path.AssetPath);
                if (!Directory.Exists($@"{basepath}\mismatches"))
                {
                    Directory.CreateDirectory($@"{basepath}\mismatches");
                }

                Console.WriteLine($@"Mismatch: {msb}");
                File.WriteAllBytes($@"{basepath}\mismatches\{Path.GetFileNameWithoutExtension(path.AssetPath)}",
                    written);
            }
            else
            {
                var basepath = Path.GetDirectoryName(path.AssetPath);
                if (Directory.Exists($@"{basepath}\mismatches"))
                {
                    if (File.Exists($@"{basepath}\mismatches\{Path.GetFileNameWithoutExtension(path.AssetPath)}"))
                    {
                        File.Delete($@"{basepath}\mismatches\{Path.GetFileNameWithoutExtension(path.AssetPath)}");
                    }
                    Directory.Delete($@"{basepath}\mismatches");
                }
            }
        }

        return true;
    }
}
