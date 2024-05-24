using static Andre.Native.ImGuiBindings;
using Microsoft.Extensions.Logging;
using SoulsFormats;
using StudioCore.Scene;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Threading.Tasks.Schedulers;
using System.Diagnostics;

namespace StudioCore.Resource;

/// <summary>
///     Manages resources (mainly GPU) such as textures and models, and can be used to unload and reload them at will.
///     A background thread will manage the unloading and streaming in of assets. This is designed to map closely to
///     the souls asset system, but in a more abstract way
/// </summary>
public static partial class ResourceManager
{
    [Flags]
    public enum ResourceType
    {
        Flver = 1,
        Texture = 2,
        CollisionHKX = 4,
        Navmesh = 8,
        NavmeshHKX = 16,
        All = 0xFFFFFFF
    }

    private static QueuedTaskScheduler JobScheduler = new(4, "JobMaster");
    private static readonly TaskFactory JobTaskFactory = new(JobScheduler);

    /// <summary>
    /// The <see cref="AssetLocator"/> for processing virtual paths.
    /// </summary>
    public static AssetLocator Locator;

    /// <summary>
    /// The database of loaded resources.
    /// </summary>
    private static readonly Dictionary<string, IResourceHandle> ResourceDatabase = new();

    /// <summary>
    /// The active jobs and their progress.
    /// </summary>
    private static readonly ConcurrentDictionary<ResourceJob, int> ActiveJobProgress = new();

    /// <summary>
    /// The files that are currently being processed.
    /// </summary>
    private static readonly HashSet<string> InFlightFiles = new();

    /// <summary>
    /// Requests to be notified of when a resource loads.
    /// </summary>
    private static readonly BufferBlock<AddResourceLoadNotificationRequest> _notificationRequests = new();

    /// <summary>
    /// Requests to unload resources.
    /// </summary>
    private static readonly BufferBlock<UnloadResourceRequest> _unloadRequests = new();

    private static int Pending = 0;
    private static int Finished = 0;
    private static int _prevCount;

    private static object AddResourceLock = new();
    private static bool AddingResource = false;

    private static bool _scheduleUDSFMLoad;
    private static bool _scheduleUnloadedTexturesLoad;

    private static bool TaskWindowOpen = true;

    private static IResourceHandle InstantiateResource(ResourceType type, string path)
    {
        switch (type)
        {
            case ResourceType.Flver:
                return new ResourceHandle<FlverResource>(path);
            //case ResourceType.Texture:
            //    return new ResourceHandle<TextureResource>(path);
        }

        return null;
    }

    private static LoadTPFTextureResourceRequest[] LoadTPFResources(LoadTPFResourcesAction action)
    {
        Tracy.___tracy_c_zone_context ctx =
            Tracy.TracyCZoneN(1, $@"LoadTPFResourcesTask::Run {action._virtpathbase}");
        if (!CFG.Current.EnableTexturing)
        {
            action._tpf = null;
            Tracy.TracyCZoneEnd(ctx);
            return new LoadTPFTextureResourceRequest[] { };
        }

        // If tpf is null this is a loose file load.
        if (action._tpf == null)
        {
            try
            {
                action._tpf = TPF.Read(action._filePath);
            }
            catch (Exception e)
            {
                TaskLogs.AddLog($"Failed to load TPF \"{action._filePath}\": {e.Message}",
                    LogLevel.Warning, TaskLogs.LogPriority.Normal, e);
                return new LoadTPFTextureResourceRequest[] { };
            }
        }

        action._job.IncrementEstimateTaskSize(action._tpf.Textures.Count);
        var ret = new LoadTPFTextureResourceRequest[action._tpf.Textures.Count];
        for (var i = 0; i < action._tpf.Textures.Count; i++)
        {
            TPF.Texture tex = action._tpf.Textures[i];
            ret[i] = new LoadTPFTextureResourceRequest($@"{action._virtpathbase}/{tex.Name}", action._tpf, i,
                action._accessLevel, action._game);
        }

        action._tpf = null;
        Tracy.TracyCZoneEnd(ctx);
        return ret;
    }

    private static void LoadBinderResources(LoadBinderResourcesAction action)
    {
        try
        {
            action.ProcessBinder();
            if (!action.PopulateResourcesOnly)
            {
                var doasync = action.PendingResources.Count() + action.PendingTPFs.Count() > 1;
                var i = 0;
                foreach (Tuple<IResourceLoadPipeline, string, BinderFileHeader> p in action.PendingResources)
                {
                    Memory<byte> f = action.Binder.ReadFile(p.Item3);
                    p.Item1.LoadByteResourceBlock.Post(new LoadByteResourceRequest(p.Item2, f, action.AccessLevel,
                        Locator.Type));
                    action._job.IncrementEstimateTaskSize(1);
                    i++;
                }

                foreach (Tuple<string, BinderFileHeader> t in action.PendingTPFs)
                {
                    try
                    {
                        TPF f = TPF.Read(action.Binder.ReadFile(t.Item2));
                        action._job.AddLoadTPFResources(new LoadTPFResourcesAction(action._job, t.Item1, f,
                            action.AccessLevel, Locator.Type));
                    }
                    catch (Exception e)
                    {
                        TaskLogs.AddLog($"Failed to load TPF \"{t.Item1}\"",
                            LogLevel.Warning, TaskLogs.LogPriority.Normal, e);
                    }

                    i++;
                }
            }
        }
        catch (Exception e)
        {
            TaskLogs.AddLog($"Failed to load binder \"{action.BinderVirtualPath}\"",
                LogLevel.Warning, TaskLogs.LogPriority.Normal, e);
        }

        action.PendingResources.Clear();
        action.Binder = null;
    }

    /// <summary>
    /// Constructs a handle containing the requested resource type and virtual path.
    /// </summary>
    /// <param name="resourceType">The type of the resource to construct of a handle for.</param>
    /// <param name="virtualpath">The virtual path to the resource.</param>
    /// <returns>A resource handle for a resource.</returns>
    /// <exception cref="NotSupportedException">The given resource type is not supported or implemented.</exception>
    private static IResourceHandle ConstructHandle(Type resourceType, string virtualpath)
    {
        if (resourceType == typeof(FlverResource))
        {
            return new ResourceHandle<FlverResource>(virtualpath);
        }

        if (resourceType == typeof(HavokCollisionResource))
        {
            return new ResourceHandle<HavokCollisionResource>(virtualpath);
        }

        if (resourceType == typeof(HavokNavmeshResource))
        {
            return new ResourceHandle<HavokNavmeshResource>(virtualpath);
        }

        if (resourceType == typeof(NVMNavmeshResource))
        {
            return new ResourceHandle<NVMNavmeshResource>(virtualpath);
        }

        if (resourceType == typeof(TextureResource))
        {
            return new ResourceHandle<TextureResource>(virtualpath);
        }

        throw new NotSupportedException("Unhandled resource type");
    }

    /// <summary>
    ///     See if you can use a target resource's access level with a given access level
    /// </summary>
    /// <param name="src">The access level you want to use a resource at</param>
    /// <param name="target">The access level the resource is at</param>
    /// <returns></returns>
    public static bool CheckAccessLevel(AccessLevel src, AccessLevel target)
    {
        // Full access level means anything can use it
        if (target == AccessLevel.AccessFull)
        {
            return true;
        }

        return src == target;
    }

    /// <summary>
    /// Creates a <see cref="BinderReader"/> for a Binder file.
    /// </summary>
    /// <param name="filePath">The path to the Binder file.</param>
    /// <param name="type">The current game type to determine which version of Binder to use.</param>
    /// <returns>A <see cref="BinderReader"/>.</returns>
    public static BinderReader InstantiateBinderReaderForFile(string filePath, GameType type)
    {
        if (filePath == null || !File.Exists(filePath))
        {
            return null;
        }

        if (type == GameType.DemonsSouls || type == GameType.DarkSoulsPTDE || type == GameType.DarkSoulsRemastered || type == GameType.ArmoredCoreVD)
        {
            if (filePath.ToUpper().EndsWith("BHD"))
            {
                return new BXF3Reader(filePath, filePath.Substring(0, filePath.Length - 3) + "bdt");
            }

            return new BND3Reader(filePath);
        }

        if (filePath.ToUpper().EndsWith("BHD"))
        {
            return new BXF4Reader(filePath, filePath.Substring(0, filePath.Length - 3) + "bdt");
        }

        return new BND4Reader(filePath);
    }

    /// <summary>
    /// Unloads resources that have nothing requesting them.
    /// </summary>
    public static void UnloadUnusedResources()
    {
        foreach (KeyValuePair<string, IResourceHandle> r in ResourceDatabase)
        {
            if (r.Value.IsLoaded() && r.Value.GetReferenceCounts() == 0)
            {
                r.Value.UnloadIfUnused();
            }
        }
    }

    /// <summary>
    /// Creates a new <see cref="ResourceJob"/> using a <see cref="ResourceJobBuilder"/>.
    /// </summary>
    /// <param name="name">The name of the job.</param>
    /// <returns>A <see cref="ResourceJobBuilder"/> for building a <see cref="ResourceJob"/>.</returns>
    public static ResourceJobBuilder CreateNewJob(string name)
    {
        return new ResourceJobBuilder(name);
    }

    /// <summary>
    ///     The primary way to get a handle to the resource, this will call the provided listener once the requested
    ///     resource is available and loaded. This will be called on the main UI thread.
    /// </summary>
    /// <param name="resourceName">The name of the requested resource.</param>
    /// <param name="listener">The listener waiting for the resource.</param>
    /// <param name="al">The requested access level to the requested resource.</param>
    public static void AddResourceListener<T>(string resourceName, IResourceEventListener listener, AccessLevel al, int tag = 0) where T : IResource
    {
        _notificationRequests.Post(new AddResourceLoadNotificationRequest(resourceName.ToLower(), typeof(T), listener, al, tag));
    }

    public static void MarkResourceInFlight(string resourceName, AccessLevel al)
    {
        // TODO is this needed?
        /*var lResourceName = resourceName.ToLower();
        if (!ResourceDatabase.ContainsKey(lResourceName))
            ResourceDatabase.Add(lResourceName, new ResourceRegistration(al));
        ResourceDatabase[lResourceName].AccessLevel = al;*/
    }

    /// <summary>
    /// Check if a resource is loaded or being loaded if the required access level is met.
    /// </summary>
    /// <param name="resourceName">The name of the resource to check.</param>
    /// <param name="al">The access level requested.</param>
    /// <returns>Whether or no a resource is loaded or being loaded.</returns>
    public static bool IsResourceLoadedOrInFlight(string resourceName, AccessLevel al)
    {
        var lResourceName = resourceName.ToLower();
        return ResourceDatabase.ContainsKey(lResourceName) && CheckAccessLevel(al, ResourceDatabase[lResourceName].AccessLevel);
    }

    public static void UnloadResource(IResourceHandle resource, bool unloadOnlyIfUnused)
    {
        _unloadRequests.Post(new UnloadResourceRequest(resource, unloadOnlyIfUnused));
    }

    public static void ScheduleUDSMFRefresh()
    {
        _scheduleUDSFMLoad = true;
    }

    public static void ScheduleUnloadedTexturesRefresh()
    {
        _scheduleUnloadedTexturesLoad = true;
    }

    /// <summary>
    /// Process the current tasks the <see cref="ResourceManager"/> has.
    /// </summary>
    public static void UpdateTasks()
    {
        // Process any resource notification requests
        if (_notificationRequests.TryReceiveAll(out IList<AddResourceLoadNotificationRequest> requests))
        {
            foreach (AddResourceLoadNotificationRequest request in requests)
            {
                // Flatten the name of the resource to lowercase for the database
                var lResourceName = request.ResourceVirtualPath.ToLower();

                // Try to get the resource from the database and add it if it doesn't already exist.
                if (!ResourceDatabase.TryGetValue(lResourceName, out IResourceHandle registration))
                {
                    registration = ConstructHandle(request.Type, request.ResourceVirtualPath);
                    ResourceDatabase.Add(lResourceName, registration);
                }

                // Add a resource listener for the resource
                registration.AddResourceEventListener(request.Listener, request.AccessLevel, request.tag);
            }
        }

        // If no loading job is currently in flight, process any unload requests
        var jobCount = ActiveJobProgress.Count;
        if (jobCount == 0)
        {
            InFlightFiles.Clear();
            if (_unloadRequests.TryReceiveAll(out IList<UnloadResourceRequest> toUnload))
            {
                foreach (UnloadResourceRequest r in toUnload)
                {
                    if (r.UnloadOnlyIfUnused && r.Resource.GetReferenceCounts() > 0)
                    {
                        continue;
                    }

                    r.Resource.Unload();
                    if (r.Resource.GetReferenceCounts() > 0)
                    {
                        continue;
                    }

                    ResourceDatabase.Remove(r.Resource.AssetVirtualPath.ToLower());
                }
            }
        }

        if (jobCount > 0)
        {
            HashSet<ResourceJob> toRemove = new();
            foreach (KeyValuePair<ResourceJob, int> job in ActiveJobProgress)
            {
                job.Key.ProcessLoadedResources();
                if (job.Key.Finished)
                {
                    toRemove.Add(job.Key);
                }
            }

            foreach (ResourceJob rm in toRemove)
            {
                ActiveJobProgress.TryRemove(rm, out _);
            }
        }
        else
        {
            // Post processing jobs
            if (Renderer.GeometryBufferAllocator != null &&
                Renderer.GeometryBufferAllocator.HasStagingOrPending())
            {
                Tracy.___tracy_c_zone_context ctx = Tracy.TracyCZoneN(1, "Flush Staging buffer");
                Renderer.GeometryBufferAllocator.FlushStaging(true);
                Tracy.TracyCZoneEnd(ctx);
            }

            if (_scheduleUDSFMLoad)
            {
                ResourceJobBuilder job = CreateNewJob(@"Loading UDSFM textures");
                job.AddLoadUDSFMTexturesTask();
                job.Complete();
                _scheduleUDSFMLoad = false;
            }

            if (_scheduleUnloadedTexturesLoad)
            {
                ResourceJobBuilder job = CreateNewJob(@"Loading other textures");
                job.AddLoadUnloadedTextures();
                job.Complete();
                _scheduleUnloadedTexturesLoad = false;
            }
        }

        // If there were active jobs last frame but none this frame, clear out unused resources
        if (_prevCount > 0 && ActiveJobProgress.Count == 0)
        {
            UnloadUnusedResources();
        }

        _prevCount = ActiveJobProgress.Count;
    }

    /// <summary>
    /// Draw the current tasks to the GUI.
    /// </summary>
    /// <param name="w">The width of the tasks window.</param>
    /// <param name="h">The height of the tasks window.</param>
    public static void OnGuiDrawTasks(float w, float h)
    {
        var scale = MapStudioNew.GetUIScale();

        if (ActiveJobProgress.Count > 0)
        {
            ImGui.SetNextWindowSize(new Vector2(400, 310) * scale);
            ImGui.SetNextWindowPos(new Vector2(w - (100 * scale), h - (300 * scale)));
            if (!ImGui.Begin("Resource Loading Tasks", ref TaskWindowOpen, ImGuiWindowFlags.NoDecoration))
            {
                ImGui.End();
                return;
            }

            foreach (KeyValuePair<ResourceJob, int> job in ActiveJobProgress)
            {
                if (!job.Key.Finished)
                {
                    var completed = job.Key.Progress;
                    var size = job.Key.GetEstimateTaskSize();
                    ImGui.Text(job.Key.Name);
                    if (size == 0)
                    {
                        ImGui.ProgressBar(0.0f);
                    }
                    else
                    {
                        ImGui.ProgressBar(completed / (float)size, new Vector2(386.0f, 20.0f) * scale);
                    }
                }
            }

            ImGui.End();
        }
    }

    /// <summary>
    /// Draw the current resource list to the GUI.
    /// </summary>
    public static void OnGuiDrawResourceList()
    {
        if (!ImGui.Begin("Resource List"))
        {
            ImGui.End();
            return;
        }

        ImGui.Text("List of Resources Loaded & Unloaded");
        ImGui.Columns(4);
        ImGui.Separator();
        var id = 0;
        foreach (KeyValuePair<string, IResourceHandle> item in ResourceDatabase)
        {
            ImGui.PushID(id);
            ImGui.AlignTextToFramePadding();
            ImGui.Text(item.Key);
            ImGui.NextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text(item.Value.IsLoaded() ? "Loaded" : "Unloaded");
            ImGui.NextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text(item.Value.AccessLevel.ToString());
            ImGui.NextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text(item.Value.GetReferenceCounts().ToString());
            ImGui.NextColumn();
            ImGui.PopID();
        }

        ImGui.Columns(1);
        ImGui.Separator();
        ImGui.End();
    }

    /// <summary>
    /// Shutdown the <see cref="ResourceManager"/>.
    /// </summary>
    public static void Shutdown()
    {
        JobScheduler.Dispose();
        JobScheduler = null;
    }

    // Unused
    public interface IResourceTask
    {
        public void Run();
        public Task RunAsync(IProgress<int> progress);

        /// <summary>
        ///     Get an estimate of the size of a task (i.e. how many files to load)
        /// </summary>
        /// <returns></returns>
        public int GetEstimateTaskSize();
    }

    internal struct LoadTPFResourcesAction
    {
        public ResourceJob _job;
        public string _virtpathbase = null;
        public TPF _tpf = null;
        public string _filePath = null;
        public AccessLevel _accessLevel = AccessLevel.AccessGPUOptimizedOnly;
        public GameType _game;

        public LoadTPFResourcesAction(ResourceJob job, string virtpathbase, TPF tpf, AccessLevel al, GameType type)
        {
            _job = job;
            _virtpathbase = virtpathbase;
            _tpf = tpf;
            _accessLevel = al;
            _game = type;
        }

        public LoadTPFResourcesAction(ResourceJob job, string virtpathbase, string filePath, AccessLevel al,
            GameType type)
        {
            _job = job;
            _virtpathbase = virtpathbase;
            _filePath = filePath;
            _accessLevel = al;
            _game = type;
        }
    }

    internal class LoadBinderResourcesAction
    {
        public readonly object ProgressLock = new();
        public ResourceJob _job;
        public AccessLevel AccessLevel = AccessLevel.AccessGPUOptimizedOnly;
        public HashSet<string> AssetWhitelist;
        public BinderReader Binder;
        public HashSet<int> BinderLoadMask = null;
        public string BinderVirtualPath;
        public List<Task> LoadingTasks = new();

        public List<Tuple<IResourceLoadPipeline, string, BinderFileHeader>> PendingResources = new();
        public List<Tuple<string, BinderFileHeader>> PendingTPFs = new();
        public bool PopulateResourcesOnly;
        public ResourceType ResourceMask = ResourceType.All;
        public List<int> TaskProgress = new();
        public List<int> TaskSizes = new();
        public int TotalSize = 0;

        public LoadBinderResourcesAction(ResourceJob job, string virtpath, AccessLevel accessLevel,
            bool populateOnly, ResourceType mask, HashSet<string> whitelist)
        {
            _job = job;
            BinderVirtualPath = virtpath;
            PopulateResourcesOnly = populateOnly;
            ResourceMask = mask;
            AssetWhitelist = whitelist;
            AccessLevel = accessLevel;
        }

        public void ProcessBinder()
        {
            if (Binder == null)
            {
                string o;
                var path = Locator.VirtualToRealPath(BinderVirtualPath, out o);
                Binder = InstantiateBinderReaderForFile(path, Locator.Type);
                if (Binder == null)
                {
                    return;
                }
            }

            for (var i = 0; i < Binder.Files.Count(); i++)
            {
                BinderFileHeader f = Binder.Files[i];
                if (BinderLoadMask != null && !BinderLoadMask.Contains(i))
                {
                    continue;
                }

                var binderpath = f.Name;
                var filevirtpath = Locator.GetBinderVirtualPath(BinderVirtualPath, binderpath);
                if (AssetWhitelist != null && !AssetWhitelist.Contains(filevirtpath))
                {
                    continue;
                }

                IResourceLoadPipeline pipeline = null;
                if (filevirtpath.ToUpper().EndsWith(".TPF") || filevirtpath.ToUpper().EndsWith(".TPF.DCX"))
                {
                    var virt = BinderVirtualPath;
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

                    PendingTPFs.Add(new Tuple<string, BinderFileHeader>(virt, f));
                }
                else
                {
                    if (ResourceMask.HasFlag(ResourceType.Flver) &&
                        (filevirtpath.ToUpper().EndsWith(".FLVER") ||
                         filevirtpath.ToUpper().EndsWith(".FLV") ||
                         filevirtpath.ToUpper().EndsWith(".FLV.DCX")))
                    {
                        //handle = new ResourceHandle<FlverResource>();
                        pipeline = _job.FlverLoadPipeline;
                    }
                    else if (ResourceMask.HasFlag(ResourceType.CollisionHKX) &&
                             (filevirtpath.ToUpper().EndsWith(".HKX") ||
                              filevirtpath.ToUpper().EndsWith(".HKX.DCX")))
                    {
                        pipeline = _job.HavokCollisionLoadPipeline;
                    }
                    else if (ResourceMask.HasFlag(ResourceType.Navmesh) && filevirtpath.ToUpper().EndsWith(".NVM"))
                    {
                        pipeline = _job.NVMNavmeshLoadPipeline;
                    }
                    else if (ResourceMask.HasFlag(ResourceType.NavmeshHKX) &&
                             (filevirtpath.ToUpper().EndsWith(".HKX") ||
                              filevirtpath.ToUpper().EndsWith(".HKX.DCX")))
                    {
                        pipeline = _job.HavokNavmeshLoadPipeline;
                    }

                    if (pipeline != null)
                    {
                        PendingResources.Add(
                            new Tuple<IResourceLoadPipeline, string, BinderFileHeader>(pipeline, filevirtpath, f));
                    }
                }
            }
        }
    }

    // Unused
    private class ResourceRegistration
    {
        public ResourceRegistration(AccessLevel al)
        {
            AccessLevel = al;
        }

        public IResourceHandle? Handle { get; set; } = null;
        public AccessLevel AccessLevel { get; set; }

        public List<AddResourceLoadNotificationRequest> NotificationRequests { get; set; } = new();
    }

    private readonly record struct AddResourceLoadNotificationRequest(
        string ResourceVirtualPath,
        Type Type,
        IResourceEventListener Listener,
        AccessLevel AccessLevel,
        int tag);

    private readonly record struct UnloadResourceRequest(
        IResourceHandle Resource,
        bool UnloadOnlyIfUnused);
}
