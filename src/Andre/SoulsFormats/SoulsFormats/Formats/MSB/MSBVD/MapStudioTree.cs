using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace SoulsFormats
{
    public partial class MSBVD
    {
        /// <summary>
        /// A bounding volume hierarchy of some kind used in culling.
        /// </summary>
        public class MapStudioTree
        {
            /// <summary>
            /// Unknown; probably some kind of version number.
            /// </summary>
            public int Version { get; set; }

            /// <summary>
            /// The Name or Type of the Param.
            /// </summary>
            private protected string Name = "MAPSTUDIO_TREE_ST";

            /// <summary>
            /// The root node of the Bounding Volume Hierarchy.
            /// </summary>
            public TreeNode RootNode { get; set; }

            public MapStudioTree()
            {
                RootNode = new TreeNode();
                Version = 10001002;
            }

            internal TreeNode Read(BinaryReaderEx br)
            {
                Version = br.ReadInt32();
                int nameOffset = br.ReadInt32();
                int offsetCount = br.ReadInt32();
                int rootNodeOffset = br.ReadInt32();
                br.Skip((offsetCount - 2) * 4); // Entry Offsets
                int nextParamOffset = br.ReadInt32();

                string name = br.GetASCII(nameOffset);
                if (name != Name)
                    throw new InvalidDataException($"Expected param \"{Name}\", got param \"{name}\"");

                if (offsetCount - 1 != 0)
                {
                    br.Position = rootNodeOffset;
                    RootNode = new TreeNode(br);
                    br.Position = nextParamOffset;
                }
                else
                {
                    RootNode = null;
                }
                return RootNode;
            }

            internal void Write(BinaryWriterEx bw)
            {
                bw.WriteInt32(Version);
                bw.ReserveInt32("ParamNameOffset");
                int count = RootNode.GetNodeCount();
                bw.WriteInt32(count + 1);
                for (int i = 0; i < count; i++)
                {
                    bw.ReserveInt32($"OffsetTreeNode_{i}");
                }
                bw.ReserveInt32("NextParamOffset");

                bw.FillInt32("ParamNameOffset", (int)bw.Position);
                bw.WriteASCII(Name, true);
                bw.Pad(4);

                int index = 0;
                RootNode.Write(bw, ref index);
            }
        }

        public class TreeNode
        {
            public BoundingBox Bounding { get; set; }
            public TreeNode Child1 { get; set; }
            public TreeNode Child2 { get; set; }
            public List<short> PartIndices { get; set; }

            public TreeNode()
            {
                Bounding = new BoundingBox();
                Child1 = null;
                Child2 = null;
            }

            internal TreeNode(BinaryReaderEx br)
            {
                long start = br.Position;
                Bounding = new BoundingBox();
                Bounding.Minimum = br.ReadVector3();
                int childOffset1 = br.ReadInt32();
                Bounding.Maximum = br.ReadVector3();
                br.AssertInt32(0); // Another child? Another Root?
                Bounding.Origin = br.ReadVector3();
                int childOffset2 = br.ReadInt32();
                Bounding.Unk = br.ReadSingle();
                int partIndexCount = br.ReadInt32();

                PartIndices = new List<short>();
                for (int i = 0; i < partIndexCount; i++)
                {
                    PartIndices.Add(br.ReadInt16());
                }

                if (childOffset1 > 0)
                {
                    br.Position = start + childOffset1;
                    Child1 = new TreeNode(br);
                }

                if (childOffset2 > 0)
                {
                    br.Position = start + childOffset2;
                    Child2 = new TreeNode(br);
                }
            }

            internal void Write(BinaryWriterEx bw, ref int index)
            {
                long start = bw.Position;
                bw.FillInt32($"OffsetTreeNode_{index}", (int)start);
                string fillStr1 = $"OffsetTreeNodeChild1_{index}";
                string fillStr2 = $"OffsetTreeNodeChild2_{index}";

                bw.WriteVector3(Bounding.Minimum);
                bw.ReserveInt32(fillStr1);
                bw.WriteVector3(Bounding.Maximum);
                bw.WriteInt32(0); // Another child? Another Root?
                bw.WriteVector3(Bounding.Origin);
                bw.ReserveInt32(fillStr2);
                bw.WriteSingle(Bounding.Unk);
                bw.WriteInt32(PartIndices.Count);
                for (int i = 0; i < PartIndices.Count; i++)
                {
                    bw.WriteInt16(PartIndices[i]);
                }
                bw.Pad(0x10);
                index += 1;

                if (Child1 != null)
                {
                    bw.FillInt32(fillStr1, (int)(bw.Position - start));
                    Child1.Write(bw, ref index);
                }
                else
                {
                    bw.FillInt32(fillStr1, 0);
                }

                if (Child2 != null)
                {
                    bw.FillInt32(fillStr2, (int)(bw.Position - start));
                    Child2.Write(bw, ref index);
                }
                else
                {
                    bw.FillInt32(fillStr2, 0);
                }
            }

            internal int GetNodeCount()
            {
                int count = 1;
                TreeNode child1 = Child1;
                TreeNode child2 = Child2;

                if (child1 != null)
                {
                    count += Child1.GetNodeCount();
                }

                if (child2 != null)
                {
                    count += Child2.GetNodeCount();
                }

                return count;
            }

            public class BoundingBox
            {
                /// <summary>
                /// The minimum extent of the bounding box.
                /// </summary>
                public Vector3 Minimum { get; set; }

                /// <summary>
                /// The maximum extent of the bounding box.
                /// </summary>
                public Vector3 Maximum { get; set; }

                /// <summary>
                /// The origin of the bounding box, calculated from the minimum and maximum extent.
                /// </summary>
                public Vector3 Origin { get; set; }

                /// <summary>
                /// The length of the vector between the origin and min/max?
                /// </summary>
                public float Unk { get; set; }

                public BoundingBox()
                {
                    Minimum = new Vector3();
                    Maximum = new Vector3();
                    Origin = new Vector3();
                    Unk = 0f;
                }

                /// <summary>
                /// Create a new bounding box with a minimum and maximum extent.
                /// </summary>
                /// <param name="min">The minimum extent of the bounding box.</param>
                /// <param name="max">The maximum extent of the bounding box.</param>
                public BoundingBox(Vector3 min, Vector3 max)
                {
                    Minimum = min;
                    Maximum = max;
                    Origin = new Vector3((min.X + max.X) / 2, (min.Y + max.Y) / 2, (min.Z + max.Z) / 2);
                    Unk = Origin.Length();
                }
            }
        }
    }
}
