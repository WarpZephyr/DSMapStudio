using StudioCore.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace StudioCore.ParamEditor;

/// <summary>
/// A list of resources, used in some FromSoftware games to find params.
/// </summary>
public class ResourceList
{
    /// <summary>
    /// A description of the resource list, usually containing a .xls file name.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The resources in this resource list level.
    /// </summary>
    public List<Resource> Resources { get; set; }

    /// <summary>
    /// The resource lists in this resource list.<br/>
    /// Only seen with one level as root.
    /// </summary>
    public List<ResourceList> ResourceLists { get; set; }

    /// <summary>
    /// Create a new and empty <see cref="ResourceList"/>.
    /// </summary>
    public ResourceList()
    {
        Description = null;
        Resources = new List<Resource>();
        ResourceLists = new List<ResourceList>();
    }

    /// <summary>
    /// Create a new <see cref="ResourceList"/> with the provided description.
    /// </summary>
    public ResourceList(string description)
    {
        Description = description;
        Resources = new List<Resource>();
        ResourceLists = new List<ResourceList>();
    }

    /// <summary>
    /// Get a list of all the params that use paramdef in the <see cref="ResourceList"/>.
    /// </summary>
    /// <returns>A list of all the params that use paramdef in the <see cref="ResourceList"/></returns>
    public HashSet<string> GetDefParams()
    {
        var paramList = new HashSet<string>();
        GetDefParams(paramList);
        return paramList;
    }

    /// <summary>
    /// Get a list of all the params that use dbp in the <see cref="ResourceList"/>.
    /// </summary>
    /// <returns>A list of all the params that use dbp in the <see cref="ResourceList"/></returns>
    public HashSet<string> GetDbpParams()
    {
        var paramList = new HashSet<string>();
        GetDbpParams(paramList);
        return paramList;
    }

    /// <summary>
    /// Populate a list of all the params that use paramdef in the <see cref="ResourceList"/>.
    /// </summary>
    private void GetDefParams(HashSet<string> paramList)
    {
        foreach (var resourceList in ResourceLists)
        {
            resourceList.GetDefParams(paramList);
        }

        foreach (var resource in Resources)
        {
            if (resource.Def != null && resource.Bin != null)
            {
                paramList.Add(resource.Bin);
            }
        }
    }

    /// <summary>
    /// Populate a list of all the params that use dbp in the <see cref="ResourceList"/>.
    /// </summary>
    private void GetDbpParams(HashSet<string> paramList)
    {
        foreach (var resourceList in ResourceLists)
        {
            resourceList.GetDbpParams(paramList);
        }

        foreach (var resource in Resources)
        {
            if (resource.Dbp != null && resource.Bin != null)
            {
                paramList.Add(resource.Bin);
            }
        }
    }

    /// <summary>
    /// Deserialize a <see cref="ResourceList"/> from an XML file.
    /// </summary>
    /// <param name="path">The path to the XML file.</param>
    /// <returns>A <see cref="ResourceList"/>.</returns>
    /// <exception cref="InvalidDataException">The root node did not exist.</exception>
    public static ResourceList DeserializeFromXml(string path)
    {
        StringBuilder sb = new StringBuilder();
        string xmlString = File.ReadAllText(path);
        for (int i = 0; i < xmlString.Length; i++)
        {
            if (XmlConvert.IsXmlChar(xmlString[i]))
            {
                sb.Append(xmlString[i]);
            }
        }

        var xml = new XmlDocument();
        xml.LoadXml(sb.ToString());
        var root = xml.SelectSingleNode("ResourceList") ?? throw new InvalidDataException("Root node does not exist in XML.");
        return DeserializeResourceListFromXml(root);
    }

    /// <summary>
    /// Deserialize a <see cref="ResourceList"/> from an <see cref="XmlNode"/>.
    /// </summary>
    /// <param name="resourceListNode">The <see cref="XmlNode"/> containing the <see cref="ResourceList"/>.</param>
    /// <returns>A <see cref="ResourceList"/>.</returns>
    private static ResourceList DeserializeResourceListFromXml(XmlNode resourceListNode)
    {
        var resourcelist = new ResourceList();
        resourcelist.Description = resourceListNode.SelectSingleNode("Desc")?.InnerText;

        var resourceListNodes = resourceListNode.SelectNodes("ResourceList");
        if (resourceListNodes != null)
        {
            foreach (XmlNode childResourceListNode in resourceListNodes)
            {
                resourcelist.ResourceLists.Add(DeserializeResourceListFromXml(childResourceListNode));
            }
        }

        var resourceNodes = resourceListNode.SelectNodes("Resource");
        if (resourceNodes != null)
        {
            foreach (XmlNode childResourceNode in resourceNodes)
            {
                resourcelist.Resources.Add(DeserializeResourceFromXml(childResourceNode));
            }
        }

        return resourcelist;
    }

    /// <summary>
    /// Deserialize a <see cref="Resource"/> from an <see cref="XmlNode"/>.
    /// </summary>
    /// <param name="resourceNode">The <see cref="XmlNode"/> containing the <see cref="Resource"/>.</param>
    /// <returns>A <see cref="Resource"/>.</returns>
    private static Resource DeserializeResourceFromXml(XmlNode resourceNode)
    {
        var resource = new Resource();
        resource.Description = resourceNode.SelectSingleNode("Desc")?.InnerText;
        resource.Bin = resourceNode.SelectSingleNode("Bin")?.InnerText;
        resource.Def = resourceNode.SelectSingleNode("Def")?.InnerText;
        resource.Dbp = resourceNode.SelectSingleNode("Dbp")?.InnerText;
        return resource;
    }

    /// <summary>
    /// A resource in a resource list.
    /// </summary>
    public class Resource
    {
        /// <summary>
        /// A description of the resource.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// The data path of the resource.
        /// </summary>
        public string? Bin { get; set; }

        /// <summary>
        /// The paramdef path of the resource if it uses one.
        /// </summary>
        public string? Def { get; set; }

        /// <summary>
        /// The dbp path of the resource if it uses one.
        /// </summary>
        public string? Dbp { get; set; }

        /// <summary>
        /// Create a new and empty <see cref="Resource"/>.
        /// </summary>
        public Resource()
        {
            Description = string.Empty;
            Bin = string.Empty;
            Def = string.Empty;
            Dbp = null;
        }

        /// <summary>
        /// Create a new <see cref="Resource"/> with the provided description.
        /// </summary>
        public Resource(string description)
        {
            Description = description;
        }
    }
}
