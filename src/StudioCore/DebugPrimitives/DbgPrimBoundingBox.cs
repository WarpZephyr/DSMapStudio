using System.Drawing;
using System.Numerics;
using Veldrid.Utilities;

namespace StudioCore.DebugPrimitives;
public class DbgPrimBoundingBox(Transform location, Vector3 localMin, Vector3 localMax, Color color) : DbgPrimWireBox(location, localMin, localMax, color)
{
    public override BoundingBox Bounds => new(new Vector3(float.MinValue), new Vector3(float.MaxValue));
}
