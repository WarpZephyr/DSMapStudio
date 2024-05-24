namespace StudioCore.Resource;

/// <summary>
///     Implementors of this interface can subscribe to a resource handle to be notified of resource load/unload events.
/// </summary>
public interface IResourceEventListener
{
    /// <summary>
    /// Called when the resource is loaded.
    /// </summary>
    /// <param name="handle">A handle to the resource.</param>
    /// <param name="tag">The tag of the resource.</param>
    public void OnResourceLoaded(IResourceHandle handle, int tag);

    /// <summary>
    /// Called when the resource is unloaded.
    /// </summary>
    /// <param name="handle">A handle to the resource.</param>
    /// <param name="tag">The tag of the resource.</param>
    public void OnResourceUnloaded(IResourceHandle handle, int tag);
}
