using SoulsFormats;
using System;
using System.IO;
using System.Threading.Tasks.Dataflow;

namespace StudioCore.Resource;

/// <summary>
/// A resource request for loading bytes containing all the necessary information to do so.
/// </summary>
/// <param name="VirtualPath">The virtual path to the resource.</param>
/// <param name="Data">The data to load.</param>
/// <param name="AccessLevel">The access level to be assigned to the resource.</param>
/// <param name="GameType">The type of game the bytes are being loaded from.</param>
public readonly record struct LoadByteResourceRequest(
    string VirtualPath,
    Memory<byte> Data,
    AccessLevel AccessLevel,
    GameType GameType);

/// <summary>
/// A resource request for loading a file containing all the necessary information to do so.
/// </summary>
/// <param name="VirtualPath">The virtual path to the resource.</param>
/// <param name="File">The path to the file.</param>
/// <param name="AccessLevel">The access level to be assigned to the resource.</param>
/// <param name="GameType">The type of game the file is being loaded from.</param>
public readonly record struct LoadFileResourceRequest(
    string VirtualPath,
    string File,
    AccessLevel AccessLevel,
    GameType GameType);

/// <summary>
/// A resource request for loading a TPF texture containing all the necessary information to do so.
/// </summary>
/// <param name="VirtualPath">The virtual path to the texture resource.</param>
/// <param name="Tpf">The loaded <see cref="TPF"/>.</param>
/// <param name="Index">The index of the texture being requested in the loaded <see cref="TPF"/>.</param>
/// <param name="AccessLevel">The access level to be assigned to the textxure resource.</param>
/// <param name="GameType">The type of game the texture is being loaded from.</param>
public readonly record struct LoadTPFTextureResourceRequest(
    string VirtualPath,
    TPF Tpf,
    int Index,
    AccessLevel AccessLevel,
    GameType GameType);

/// <summary>
/// A resource loaded reply containing the resource and the information needed to register it in a resource database.
/// </summary>
/// <param name="VirtualPath">The virtual path to the resource.</param>
/// <param name="AccessLevel">The access level to be assigned to the resource.</param>
/// <param name="Resource">The resource itself.</param>
public readonly record struct ResourceLoadedReply(
    string VirtualPath,
    AccessLevel AccessLevel,
    IResource Resource);

/// <summary>
/// A resource pipeline that transforms requests for resources into replies containing resources.
/// </summary>
public interface IResourceLoadPipeline
{
    public ITargetBlock<LoadByteResourceRequest> LoadByteResourceBlock { get; }
    public ITargetBlock<LoadFileResourceRequest> LoadFileResourceRequest { get; }
    public ITargetBlock<LoadTPFTextureResourceRequest> LoadTPFTextureResourceRequest { get; }
}

/// <summary>
/// A resource pipeline that transforms requests for resources into replies containing resources.
/// </summary>
/// <typeparam name="TResource">The type of resource being requested.</typeparam>
public class ResourceLoadPipeline<TResource> : IResourceLoadPipeline where TResource : class, IResource, new()
{
    /// <summary>
    /// Loaded resource replies.
    /// </summary>
    private readonly ITargetBlock<ResourceLoadedReply> _loadedResources;

    /// <summary>
    /// Requests for resources that requires byte loading.
    /// </summary>
    private readonly ActionBlock<LoadByteResourceRequest> _loadByteResourcesTransform;

    /// <summary>
    /// Requests for resources that requires file loading.
    /// </summary>
    private readonly ActionBlock<LoadFileResourceRequest> _loadFileResourcesTransform;

    /// <summary>
    /// Requests for resources that requires byte loading.
    /// </summary>
    public ITargetBlock<LoadByteResourceRequest> LoadByteResourceBlock => _loadByteResourcesTransform;

    /// <summary>
    /// Requests for resources that requires file loading.
    /// </summary>
    public ITargetBlock<LoadFileResourceRequest> LoadFileResourceRequest => _loadFileResourcesTransform;

    /// <summary>
    /// Not used here.
    /// </summary>
    public ITargetBlock<LoadTPFTextureResourceRequest> LoadTPFTextureResourceRequest => throw new NotImplementedException();

    /// <summary>
    /// Create a new <see cref="ResourceLoadPipeline{TResource}"/> that posts resource replies to the given target.
    /// </summary>
    /// <param name="target">The target to post resource replies to.</param>
    public ResourceLoadPipeline(ITargetBlock<ResourceLoadedReply> target)
    {
        var options = new ExecutionDataflowBlockOptions();
        options.MaxDegreeOfParallelism = 6;
        _loadedResources = target;

        // Transform byte load requests into loaded replies
        _loadByteResourcesTransform = new ActionBlock<LoadByteResourceRequest>(r =>
        {
            var res = new TResource();
            res.VirtualPath = r.VirtualPath;
            var success = res._Load(r.Data, r.AccessLevel, r.GameType);
            if (success)
            {
                _loadedResources.Post(new ResourceLoadedReply(r.VirtualPath, r.AccessLevel, res));
            }
        }, options);

        // Transform file load requests into loaded replies
        _loadFileResourcesTransform = new ActionBlock<LoadFileResourceRequest>(r =>
        {
            try
            {
                var res = new TResource();
                res.VirtualPath = r.VirtualPath;
                var success = res._Load(r.File, r.AccessLevel, r.GameType);
                if (success)
                {
                    _loadedResources.Post(new ResourceLoadedReply(r.VirtualPath, r.AccessLevel, res));
                }
            }
            catch (FileNotFoundException e1) { TaskLogs.AddLog("Resource load error", Microsoft.Extensions.Logging.LogLevel.Warning, TaskLogs.LogPriority.Low, e1); }
            catch (DirectoryNotFoundException e2) { TaskLogs.AddLog("Resource load error", Microsoft.Extensions.Logging.LogLevel.Warning, TaskLogs.LogPriority.Low, e2); }
            // Some DSR FLVERS can't be read due to mismatching layout and vertex sizes
            catch (InvalidDataException e3) { TaskLogs.AddLog("Resource load error", Microsoft.Extensions.Logging.LogLevel.Warning, TaskLogs.LogPriority.Low, e3); }
        }, options);
    }

    
}

/// <summary>
/// A resource pipeline that transforms requests for texture resources into replies containing texture resources.
/// </summary>
public class TextureLoadPipeline : IResourceLoadPipeline
{
    /// <summary>
    /// Loaded resource replies.
    /// </summary>
    private readonly ITargetBlock<ResourceLoadedReply> _loadedResources;

    /// <summary>
    /// Requests for resources that require TPF loading.
    /// </summary>
    private readonly ActionBlock<LoadTPFTextureResourceRequest> _loadTPFResourcesTransform;

    /// <summary>
    /// Not used here.
    /// </summary>
    public ITargetBlock<LoadByteResourceRequest> LoadByteResourceBlock => throw new NotImplementedException();

    /// <summary>
    /// Not used here.
    /// </summary>
    public ITargetBlock<LoadFileResourceRequest> LoadFileResourceRequest => throw new NotImplementedException();

    /// <summary>
    /// Requests for resources that require TPF loading.
    /// </summary>
    public ITargetBlock<LoadTPFTextureResourceRequest> LoadTPFTextureResourceRequest => _loadTPFResourcesTransform;

    /// <summary>
    /// Create a new <see cref="TextureLoadPipeline"/> that posts resource replies to the given target.
    /// </summary>
    /// <param name="target">The target to post resource replies to.</param>
    public TextureLoadPipeline(ITargetBlock<ResourceLoadedReply> target)
    {
        var options = new ExecutionDataflowBlockOptions();
        options.MaxDegreeOfParallelism = 6;
        _loadedResources = target;
        _loadTPFResourcesTransform = new ActionBlock<LoadTPFTextureResourceRequest>(r =>
        {
            var res = new TextureResource(r.Tpf, r.Index);
            if (res._LoadTexture(r.AccessLevel))
            {
                _loadedResources.Post(new ResourceLoadedReply(r.VirtualPath, r.AccessLevel, res));
            }
        }, options);
    }

    
}
