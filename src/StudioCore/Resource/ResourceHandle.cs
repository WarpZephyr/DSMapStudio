using System;
using System.Collections.Generic;

namespace StudioCore.Resource;

/// <summary>
///     Requested access level to a given resource
/// </summary>
public enum AccessLevel
{
    /// <summary>
    ///     Resource is not loaded
    /// </summary>
    AccessUnloaded,

    /// <summary>
    ///     Access to this resource is intended for low level editing only
    /// </summary>
    AccessEditOnly,

    /// <summary>
    ///     Access to this resource is intended to be from the GPU in an optimized form
    ///     only, and is not intended to be mutated or read from the CPU. Used for textures
    ///     and models that aren't being edited primarily.
    /// </summary>
    AccessGPUOptimizedOnly,

    /// <summary>
    ///     This resource is intended to be accessed by both the GPU and accessed/modified
    ///     by the CPU.
    /// </summary>
    AccessFull
}

/// <summary>
///     A handle to a resource, which may or may not be loaded.<br/>
///     Once a resource is unloaded, it may not be reloaded with this handle and a new one must be constructed.
/// </summary>
public interface IResourceHandle
{
    /// <summary>
    /// The virtual path to the entire asset of the resource.
    /// </summary>
    public string AssetVirtualPath { get; }

    /// <summary>
    /// The current access level of the resource.
    /// </summary>
    public AccessLevel AccessLevel { get; }

    /// <summary>
    /// The number of listeners for the resource.
    /// </summary>
    public int EventListenerCount { get; }

    /// <summary>
    ///     Adds a handler that is called every time this resource is loaded.<br/>
    ///     If the resource is loaded at the time this handler is added, the handler is called immediately.<br/>
    ///     To prevent deadlock, these handlers should not trigger a load/unload of the resource.
    /// </summary>
    /// <param name="listener">The listener to add.</param>
    /// <param name="accessLevel">The access level requested by the listener.</param>
    /// <param name="tag">The tag to be provided to the listener later.</param>
    public void AddResourceEventListener(IResourceEventListener listener, AccessLevel accessLevel, int tag = 0);

    /// <summary>
    /// Remove a resource event listener.
    /// </summary>
    /// <param name="listener">The listener to remove.</param>
    public void RemoveResourceEventListener(IResourceEventListener listener);

    /// <summary>
    /// Notifies listeners that the resource has been loaded.<br/>
    /// Should only be used by ResourceManager.
    /// </summary>
    /// <param name="resource">The resource that was loaded to be passed to listeners for use.</param>
    /// <param name="accessLevel">The access level required to be notified that this resource was loaded.</param>
    public void _ResourceLoaded(IResource resource, AccessLevel accessLevel);

    /// <summary>
    ///     Requests the resource be unloaded and notifies all it's users.
    /// </summary>
    public void Unload();

    /// <summary>
    /// Requests the resource be unloaded if it is unused.
    /// </summary>
    public void UnloadIfUnused();

    /// <summary>
    /// Whether or not the resource is loaded.
    /// </summary>
    /// <returns>Whether or not the resource is loaded.</returns>
    public bool IsLoaded();

    /// <summary>
    /// Gets the number of references to the resource.
    /// </summary>
    /// <returns>The number of references to the resource.</returns>
    public int GetReferenceCounts();

    /// <summary>
    /// Acquires the resource.
    /// </summary>
    public void Acquire();

    /// <summary>
    /// Releases the resource.
    /// </summary>
    public void Release();
}

/// <summary>
///     A handle to a resource, which may or may not be loaded.<br/>
///     Once a resource is unloaded, it may not be reloaded with this handle and a new one must be constructed.
/// </summary>
/// <typeparam name="TResource">The type of resource that is wrapped by this handler.</typeparam>
public class ResourceHandle<TResource> : IResourceHandle where TResource : class, IResource, IDisposable, new()
{
    /// <summary>
    /// Listeners for events that occur in the handle.
    /// </summary>
    protected List<EventListener> EventListeners = new();

    /// <summary>
    /// The number of references to this resource.
    /// </summary>
    protected int ReferenceCount;

    /// <summary>
    /// The resource.
    /// </summary>
    protected TResource Resource;

    /// <summary>
    /// Whether or not the resource is loaded.
    /// </summary>
    public bool IsLoaded { get; protected set; }

    /// <summary>
    ///     Virtual path of the entire asset. Used to implement loading.
    /// </summary>
    public string AssetVirtualPath { get; }

    /// <summary>
    /// The access level to the resource.
    /// </summary>
    public AccessLevel AccessLevel { get; protected set; } = AccessLevel.AccessUnloaded;

    /// <summary>
    /// The number of event listeners for this resource.
    /// </summary>
    public int EventListenerCount => EventListeners.Count;

    /// <summary>
    /// Construct a new handle to a resource using the given virtual path.
    /// </summary>
    /// <param name="virtualPath">The virtual path to the resource.</param>
    public ResourceHandle(string virtualPath)
    {
        AssetVirtualPath = virtualPath;
    }

    /// <summary>
    ///     Adds a handler that is called every time this resource is loaded.<br/>
    ///     If the resource is loaded at the time this handler is added, the handler is called immediately.<br/>
    ///     To prevent deadlock, these handlers should not trigger a load/unload of the resource.
    /// </summary>
    /// <param name="listener">The listener to add.</param>
    /// <param name="accessLevel">The access level requested by the listener.</param>
    /// <param name="tag">The tag to be provided to the listener later.</param>
    public void AddResourceEventListener(IResourceEventListener listener, AccessLevel accessLevel, int tag = 0)
    {
        EventListeners.Add(new EventListener(
            new WeakReference<IResourceEventListener>(listener), accessLevel, tag));

        if (IsLoaded)
        {
            if (ResourceManager.CheckAccessLevel(accessLevel, AccessLevel))
            {
                listener.OnResourceLoaded(this, tag);
            }
        }
    }

    /// <summary>
    /// Remove a resource event listener.<br/>
    /// Not yet implemented.
    /// </summary>
    /// <param name="listener">The listener to remove.</param>
    public void RemoveResourceEventListener(IResourceEventListener listener)
    {
        // To implement
    }

    /// <summary>
    /// Notifies listeners that the resource has been loaded.<br/>
    /// Should only be used by ResourceManager.
    /// </summary>
    /// <param name="resource">The resource that was loaded to be passed to listeners for use.</param>
    /// <param name="accessLevel">The access level required to be notified that this resource was loaded.</param>
    public void _ResourceLoaded(IResource resource, AccessLevel accessLevel)
    {
        // If there's already a resource make sure it's unloaded and everyone notified
        Unload();

        Resource = (TResource)resource;
        AccessLevel = accessLevel;
        IsLoaded = true;

        foreach (EventListener listener in EventListeners)
        {
            if (listener.Listener.TryGetTarget(out IResourceEventListener l))
            {
                if (ResourceManager.CheckAccessLevel(listener.AccessLevel, accessLevel))
                {
                    l.OnResourceLoaded(this, listener.Tag);
                }
            }
        }
    }

    /// <summary>
    ///     Unloads the resource by notifying all the users and then scheduling it for deletion in the resource manager.
    /// </summary>
    public void Unload()
    {
        if (Resource == null)
        {
            return;
        }

        foreach (EventListener listener in EventListeners)
        {
            if (listener.Listener.TryGetTarget(out IResourceEventListener l))
            {
                l.OnResourceUnloaded(this, listener.Tag);
            }
        }

        TResource handle = Resource;
        Resource = null;
        IsLoaded = false;
        handle.Dispose();
    }

    /// <summary>
    /// Requests the <see cref="ResourceManager"/> unload the resource if it is unused.
    /// </summary>
    public void UnloadIfUnused()
    {
        if (ReferenceCount <= 0)
        {
            ResourceManager.UnloadResource(this, true);
        }
    }

    /// <summary>
    /// Whether or not the resource is loaded.
    /// </summary>
    /// <returns>Whether or not the resource is loaded.</returns>
    bool IResourceHandle.IsLoaded()
    {
        return IsLoaded;
    }

    /// <summary>
    /// Gets the number of references to the resource.
    /// </summary>
    /// <returns>The number of references to the resource.</returns>
    public int GetReferenceCounts()
    {
        return ReferenceCount;
    }

    /// <summary>
    /// Add a reference to this resource.
    /// </summary>
    public void Acquire()
    {
        ReferenceCount++;
    }

    /// <summary>
    /// Remove a reference to this resource.
    /// </summary>
    /// <exception cref="Exception">Throws if the resource was not being used by anything and this was called.</exception>
    public void Release()
    {
        ReferenceCount--;
        if (ReferenceCount < 0)
        {
            throw new Exception($@"Resource {AssetVirtualPath} reference count already 0");
        }

        if (ReferenceCount == 0 && IsLoaded)
        {
            ResourceManager.UnloadResource(this, true);
        }
    }

    /// <summary>
    /// Get the resource in this handle.
    /// </summary>
    /// <returns>The resource.</returns>
    public TResource Get()
    {
        return Resource;
    }

    /// <summary>
    ///     Constructs a temporary handle for a resource. This is useful for creating "fake"
    ///     resources that aren't serialized and registered with the resource management system
    ///     yet such as previews for freshly imported models
    /// </summary>
    /// <param name="res">The resource to create a handle from</param>
    /// <returns>A temporary handle to the resource.</returns>
    public static ResourceHandle<TResource> TempHandleFromResource(TResource res)
    {
        var ret = new ResourceHandle<TResource>("temp");
        ret.AccessLevel = AccessLevel.AccessFull;
        ret.IsLoaded = true;
        ret.Resource = res;
        return ret;
    }

    public override string ToString()
    {
        return AssetVirtualPath;
    }

    /// <summary>
    /// An event listener that holds onto the information necessary to notify a listener.
    /// </summary>
    /// <param name="Listener">A listener.</param>
    /// <param name="AccessLevel">The access level to be passed to the listener.</param>
    /// <param name="Tag">The tag to be passed to the listener.</param>
    protected readonly record struct EventListener(
        WeakReference<IResourceEventListener> Listener,
        AccessLevel AccessLevel,
        int Tag);
}
