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
#if MSB_READ_WRITE_TEST_LOG_ON_CRASH
            try
            {
#endif
                var bytes = File.ReadAllBytes(path.AssetPath);
                MSBVD m = MSBVD.Read(bytes);
                var written = m.Write(DCX.Type.None);
                var basepath = Path.GetDirectoryName(path.AssetPath).Replace("map", "map_test_mismatches");
                if (!bytes.AsMemory().Span.SequenceEqual(written))
                {
                    Directory.CreateDirectory(basepath);
                    File.WriteAllBytes($@"{basepath}\{Path.GetFileNameWithoutExtension(path.AssetPath)}",
                        written);
                }
                else
                {
                    if (Directory.Exists(basepath))
                    {
                        if (File.Exists($@"{basepath}\{Path.GetFileNameWithoutExtension(path.AssetPath)}"))
                        {
                            File.Delete($@"{basepath}\{Path.GetFileNameWithoutExtension(path.AssetPath)}");
                        }

                        if (Directory.GetFiles(basepath).Length == 0)
                        {
                            Directory.Delete(basepath);
                        }
                    }
                }
#if MSB_READ_WRITE_TEST_LOG_ON_CRASH
        }
            catch (Exception e)
            {
                TaskLogs.AddLog($"Failed testing ACVD MSB: {path.AssetPath}\n{e}", Microsoft.Extensions.Logging.LogLevel.Debug);
            }
#endif
        }

        return true;
    }
}
