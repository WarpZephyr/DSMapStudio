using DotNext.IO.MemoryMappedFiles;
using SoulsFormats;
using StudioCore.Resource;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace StudioCore.Tests;
public static class ModelReadWrite
{
    public static bool RunReadACVD(AssetLocator locator)
    {
        var resource = new FlverResource();

        string root = locator.GameRootDirectory;
        string modelDir = root + @"\model";
        var files = Directory.EnumerateFiles(modelDir, "*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            // Leftover ACFA FLVER0s
            if (file.Contains("break"))
            {
                continue;
            }

            if (BND3.Is(file))
            {
                var bnd = BND3.Read(file);
                foreach (var bfile in bnd.Files)
                {
                    if (bfile.Name.EndsWith(".flv") || bfile.Name.EndsWith(".flv.dcx"))
                    {
                        resource._Load(bfile.Bytes, AccessLevel.AccessGPUOptimizedOnly, GameType.ArmoredCoreVD);
                        resource = new FlverResource();
                    }
                }
            }
            else
            {
                if (file.EndsWith(".flv") || file.EndsWith(".flv.dcx"))
                {
                    var mmf = MemoryMappedFile.CreateFromFile(file, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
                    var accessor = mmf.CreateMemoryAccessor(0, 0, MemoryMappedFileAccess.Read);
                    resource._Load(accessor.Memory, AccessLevel.AccessGPUOptimizedOnly, GameType.ArmoredCoreVD);
                    resource = new FlverResource();
                }
            }
        }

        return true;
    }

    public static bool RunReadACVD(AssetLocator locator, string path, string name)
    {
        var resource = new FlverResource();

        if (BND3.Is(path))
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var bnd = BND3.Read(path);
            foreach (var bfile in bnd.Files)
            {
                if (bfile.Name.EndsWith(".flv") || bfile.Name.EndsWith(".flv.dcx") && bfile.Name.Contains(name))
                {
                    resource._Load(bfile.Bytes, AccessLevel.AccessGPUOptimizedOnly, GameType.ArmoredCoreVD);
                    return true;
                }
            }
        }
        else
        {
            var mmf = MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            var accessor = mmf.CreateMemoryAccessor(0, 0, MemoryMappedFileAccess.Read);
            resource._Load(accessor.Memory, AccessLevel.AccessGPUOptimizedOnly, GameType.ArmoredCoreVD);
            return true;
        }

        return false;
    }
}
