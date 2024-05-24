using System;

namespace StudioCore.Resource;

/// <summary>
/// An interface for a resource.
/// </summary>
public interface IResource
{
    /// <summary>
    /// The virtual path to the resource.
    /// </summary>
    public string VirtualPath { get; set; }

    /// <summary>
    /// Load the resource from bytes.
    /// </summary>
    /// <param name="bytes">The bytes of the resource.</param>
    /// <param name="al">The access level of the resource.</param>
    /// <param name="type">The type of game this resource is being loaded from.</param>
    /// <returns>Whether or not the load was successful.</returns>
    public bool _Load(Memory<byte> bytes, AccessLevel al, GameType type);

    /// <summary>
    /// Load the resource from a file.
    /// </summary>
    /// <param name="file">The path to the file of the resource.</param>
    /// <param name="al">The access level of the resource.</param>
    /// <param name="type">The type of game this resource is being loaded from.</param>
    /// <returns>Whether or not the load was successful.</returns>
    public bool _Load(string file, AccessLevel al, GameType type);
}
