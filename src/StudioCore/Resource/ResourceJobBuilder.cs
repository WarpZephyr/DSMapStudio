using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks.Dataflow;
using System.Threading.Tasks;

namespace StudioCore.Resource;
public static partial class ResourceManager
{
    /// <summary>
    /// A builder that assists in building a <see cref="ResourceJob"/> by abstracting request making.
    /// </summary>
    public class ResourceJobBuilder
    {
        /// <summary>
        /// The underlying <see cref="ResourceJob"/> to be built.
        /// </summary>
        private readonly ResourceJob _job;

        /// <summary>
        /// Create a new <see cref="ResourceJobBuilder"/> to assist in building a <see cref="ResourceJob"/>.
        /// </summary>
        /// <param name="name">The name of the <see cref="ResourceJob"/>.</param>
        public ResourceJobBuilder(string name)
        {
            _job = new ResourceJob(name);
        }

        /// <summary>
        /// Requests that the <see cref="ResourceJob"/> load an archive.
        /// </summary>
        /// <param name="virtualPath">The virtual path to the archive to be processed by an <see cref="AssetLocator"/>.</param>
        /// <param name="al">The <see cref="AccessLevel"/> files in this archive will have as resources.</param>
        public void AddLoadArchiveTask(string virtualPath, AccessLevel al, bool populateOnly, HashSet<string> assets = null)
        {
            // If we are already loading this file, don't load it again.
            if (InFlightFiles.Contains(virtualPath))
            {
                return;
            }

            // Add the file to the in flight files so we know if it's already being loaded later.
            InFlightFiles.Add(virtualPath);
            if (virtualPath == "null")
            {
                return;
            }

            _job.AddLoadBinderResources(new LoadBinderResourcesAction(_job, virtualPath, al, populateOnly, ResourceType.All, assets));
        }

        /// <summary>
        /// Requests that the <see cref="ResourceJob"/> load an archive.
        /// </summary>
        /// <param name="virtualPath">The virtual path to the archive to be processed by an <see cref="AssetLocator"/>.</param>
        /// <param name="al">The <see cref="AccessLevel"/> files in this archive will have as resources.</param>
        public void AddLoadArchiveTask(string virtualPath, AccessLevel al, bool populateOnly, ResourceType filter, HashSet<string> assets = null)
        {
            // If we are already loading this file, don't load it again.
            if (InFlightFiles.Contains(virtualPath))
            {
                return;
            }

            // Add the file to the in flight files so we know if it's already being loaded later.
            InFlightFiles.Add(virtualPath);
            if (virtualPath == "null")
            {
                return;
            }

            _job.AddLoadBinderResources(new LoadBinderResourcesAction(_job, virtualPath, al, populateOnly, filter, assets));
        }

        /// <summary>
        ///     Loads a loose virtual file.
        /// </summary>
        /// <param name="virtualPath">The virtual path to the file to be processed by an <see cref="AssetLocator"/>.</param>
        /// <param name="al">The <see cref="AccessLevel"/> this file will have as a resource.</param>
        public void AddLoadFileTask(string virtualPath, AccessLevel al)
        {
            // If we are already loading this file, don't load it again.
            if (InFlightFiles.Contains(virtualPath))
            {
                return;
            }

            // Add the file to the in flight files so we know if it's already being loaded later.
            InFlightFiles.Add(virtualPath);

            var path = Locator.VirtualToRealPath(virtualPath, out string bndout);
            if (path == null || virtualPath == "null")
            {
                return;
            }

            IResourceLoadPipeline pipeline;
            if (virtualPath.EndsWith(".hkx"))
            {
                pipeline = _job.HavokCollisionLoadPipeline;
            }
            else if (path.ToUpper().EndsWith(".TPF") || path.ToUpper().EndsWith(".TPF.DCX"))
            {
                var virt = virtualPath;
                if (virt.StartsWith(@"map/tex"))
                {
                    Regex regex = new(@"\d{4}$");
                    if (regex.IsMatch(virt))
                    {
                        virt = virt.Substring(0, virt.Length - 5);
                    }
                    else if (virt.EndsWith("tex"))
                    {
                        virt = virt.Substring(0, virt.Length - 4);
                    }
                }

                _job.AddLoadTPFResources(new LoadTPFResourcesAction(_job, virt, path, al, Locator.Type));
                return;
            }
            else
            {
                pipeline = _job.FlverLoadPipeline;
            }

            pipeline.LoadFileResourceRequest.Post(new LoadFileResourceRequest(virtualPath, path, al, Locator.Type));
        }

        /// <summary>
        ///     Attempts to load unloaded resources (with active references) via UDSFM textures
        /// </summary>
        public void AddLoadUDSFMTexturesTask()
        {
            foreach (KeyValuePair<string, IResourceHandle> r in ResourceDatabase)
            {
                if (!r.Value.IsLoaded())
                {
                    var texpath = r.Key;
                    string path = null;
                    if (texpath.StartsWith("map/tex"))
                    {
                        path = $@"{Locator.GameRootDirectory}\map\tx\{Path.GetFileName(texpath)}.tpf";
                    }

                    if (path != null && File.Exists(path))
                    {
                        _job.AddLoadTPFResources(new LoadTPFResourcesAction(_job,
                            Path.GetDirectoryName(r.Key).Replace('\\', '/'),
                            path, AccessLevel.AccessGPUOptimizedOnly, Locator.Type));
                    }
                }
            }
        }

        /// <summary>
        ///     Looks for unloaded textures and queues them up for loading. References to parts and Elden Ring AETs depend on this
        /// </summary>
        public void AddLoadUnloadedTextures()
        {
            HashSet<string> assetTpfs = new();
            foreach (KeyValuePair<string, IResourceHandle> r in ResourceDatabase)
            {
                if (!r.Value.IsLoaded())
                {
                    var texpath = r.Key;
                    string path = null;
                    if (texpath.StartsWith("aet/"))
                    {
                        var splits = texpath.Split('/');
                        var aetid = splits[1];
                        var aetname = splits[2];
                        var fullaetid = aetname.Substring(0, 10);

                        if (assetTpfs.Contains(fullaetid))
                        {
                            continue;
                        }

                        path = Locator.GetAetTexture(fullaetid).AssetPath;

                        assetTpfs.Add(fullaetid);
                    }

                    if (path != null && File.Exists(path))
                    {
                        _job.AddLoadTPFResources(new LoadTPFResourcesAction(_job,
                            Path.GetDirectoryName(r.Key).Replace('\\', '/'), path,
                            AccessLevel.AccessGPUOptimizedOnly, Locator.Type));
                    }
                }
            }
        }

        /// <summary>
        /// Complete the builder and get a <see cref="Task"/> that represents the completion of the <see cref="ResourceJob"/>.
        /// </summary>
        /// <returns>A <see cref="Task"/> that represents the completion of the <see cref="ResourceJob"/></returns>
        public Task Complete()
        {
            // Build the job, register it with the task manager, and start it
            ActiveJobProgress[_job] = 0;
            Task jobtask = _job.Complete();
            return jobtask;
        }
    }
}
