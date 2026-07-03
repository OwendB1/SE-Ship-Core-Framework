using System;
using System.Collections.Generic;
using System.IO;
using VRageMath;

namespace ShipCoreFramework
{
    internal sealed class ScfMinimalMwmModel
    {
        internal readonly Dictionary<string, ScfMinimalMwmScreenAreaGeometry> AreasByMaterial =
            new Dictionary<string, ScfMinimalMwmScreenAreaGeometry>(StringComparer.OrdinalIgnoreCase);

        internal readonly List<ScfMinimalMwmLod> Lods = new List<ScfMinimalMwmLod>();
        internal int Version;
    }

    internal sealed class ScfMinimalMwmLod
    {
        internal float Distance;
        internal string Path;
    }

    internal sealed class ScfMinimalMwmScreenAreaGeometry
    {
        private bool _hasBounds;
        private bool _hasUvBounds;
        private Vector3 _min;
        private Vector3 _max;
        private Vector2 _uvMin;
        private Vector2 _uvMax;

        internal readonly List<int> TriangleIndices = new List<int>();
        internal readonly List<ScfMinimalMwmUvTriangle> UvTriangles = new List<ScfMinimalMwmUvTriangle>();
        internal readonly Dictionary<int, Vector2> UvByVertexIndex = new Dictionary<int, Vector2>();
        internal string Material;
        internal int TriangleCount;
        internal double AreaM2;
        internal bool HasUvBounds { get { return _hasUvBounds; } }
        internal Vector2 UvMin { get { return _uvMin; } }
        internal Vector2 UvMax { get { return _uvMax; } }

        internal double WidthM
        {
            get
            {
                return _hasBounds ? Math.Max(Math.Max(_max.X - _min.X, _max.Y - _min.Y), _max.Z - _min.Z) : 0d;
            }
        }

        internal double HeightM
        {
            get
            {
                if (!_hasBounds) return 0d;

                float x = _max.X - _min.X;
                float y = _max.Y - _min.Y;
                float z = _max.Z - _min.Z;
                if (x > y) Swap(ref x, ref y);
                if (y > z) Swap(ref y, ref z);
                if (x > y) Swap(ref x, ref y);
                return y;
            }
        }

        internal void AddTriangles(Vector3[] vertices, Vector2[] uvs, int[] indices, int triangleOffset)
        {
            if (vertices == null || indices == null) return;

            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                int localTriangle = i / 3;
                TriangleIndices.Add(triangleOffset + localTriangle);

                int i0 = indices[i];
                int i1 = indices[i + 1];
                int i2 = indices[i + 2];

                if (i0 < 0 || i1 < 0 || i2 < 0 ||
                    i0 >= vertices.Length || i1 >= vertices.Length || i2 >= vertices.Length)
                    continue;

                Vector3 a = vertices[i0];
                Vector3 b = vertices[i1];
                Vector3 c = vertices[i2];

                if (uvs != null && i0 < uvs.Length && i1 < uvs.Length && i2 < uvs.Length)
                {
                    Vector2 uvA = uvs[i0];
                    Vector2 uvB = uvs[i1];
                    Vector2 uvC = uvs[i2];
                    UvTriangles.Add(new ScfMinimalMwmUvTriangle(uvA, uvB, uvC));
                    AddVertexUv(i0, uvA);
                    AddVertexUv(i1, uvB);
                    AddVertexUv(i2, uvC);
                    IncludeUv(uvA);
                    IncludeUv(uvB);
                    IncludeUv(uvC);
                }
                else
                {
                    UvTriangles.Add(new ScfMinimalMwmUvTriangle(Vector2.Zero, Vector2.Zero, Vector2.Zero));
                }

                AreaM2 += Vector3.Cross(b - a, c - a).Length() * 0.5d;
                TriangleCount++;
                Include(a);
                Include(b);
                Include(c);
            }
        }

        private void Include(Vector3 value)
        {
            if (!_hasBounds)
            {
                _min = value;
                _max = value;
                _hasBounds = true;
                return;
            }

            _min = Vector3.Min(_min, value);
            _max = Vector3.Max(_max, value);
        }

        private void IncludeUv(Vector2 value)
        {
            if (!_hasUvBounds)
            {
                _uvMin = value;
                _uvMax = value;
                _hasUvBounds = true;
                return;
            }

            _uvMin = Vector2.Min(_uvMin, value);
            _uvMax = Vector2.Max(_uvMax, value);
        }

        private void AddVertexUv(int vertexIndex, Vector2 uv)
        {
            if (!UvByVertexIndex.ContainsKey(vertexIndex))
                UvByVertexIndex[vertexIndex] = uv;
        }

        private static void Swap(ref float a, ref float b)
        {
            float t = a;
            a = b;
            b = t;
        }
    }

    internal struct ScfMinimalMwmUvTriangle
    {
        internal readonly Vector2 A;
        internal readonly Vector2 B;
        internal readonly Vector2 C;

        internal ScfMinimalMwmUvTriangle(Vector2 a, Vector2 b, Vector2 c)
        {
            A = a;
            B = b;
            C = c;
        }
    }

    internal static class ScfMinimalMwmReader
    {
        private const int IndexedTagVersion = 1066002;

        internal static bool TryReadScreenArea(BinaryReader reader, string materialName,
            out ScfMinimalMwmScreenAreaGeometry geometry)
        {
            geometry = null;
            if (string.IsNullOrWhiteSpace(materialName)) return false;

            ScfMinimalMwmModel model;
            if (!TryRead(reader, materialName, out model)) return false;

            return model.AreasByMaterial.TryGetValue(materialName, out geometry) &&
                   geometry != null &&
                   geometry.TriangleCount > 0;
        }

        internal static bool TryReadLodPaths(BinaryReader reader, out List<string> lodPaths)
        {
            lodPaths = null;

            ScfMinimalMwmModel model;
            if (!TryRead(reader, null, out model) || model == null || model.Lods.Count == 0)
                return false;

            lodPaths = new List<string>(model.Lods.Count);
            for (int i = 0; i < model.Lods.Count; i++)
            {
                if (model.Lods[i] != null && !string.IsNullOrWhiteSpace(model.Lods[i].Path))
                    lodPaths.Add(model.Lods[i].Path);
            }

            return lodPaths.Count > 0;
        }

        private static bool TryRead(BinaryReader reader, string targetMaterial, out ScfMinimalMwmModel model)
        {
            model = null;
            if (reader == null || !reader.BaseStream.CanSeek) return false;

            ScfMinimalMwmModel result = new ScfMinimalMwmModel();
            try
            {
                string debugTag = reader.ReadString();
                if (!string.Equals(debugTag, "Debug", StringComparison.Ordinal)) return false;

                string[] debugLines = ReadStringArray(reader);
                result.Version = ReadVersion(debugLines);
                if (result.Version < IndexedTagVersion) return false;

                Dictionary<string, int> tags = ReadIndexDictionary(reader);

                int lodsOffset;
                if (tags.TryGetValue("LODs", out lodsOffset))
                    TryReadLodsTag(reader, lodsOffset, result.Lods);

                int verticesOffset;
                int meshPartsOffset;
                int texCoordsOffset;
                int patternScaleOffset;
                if (!tags.TryGetValue("Vertices", out verticesOffset) ||
                    !tags.TryGetValue("MeshParts", out meshPartsOffset))
                {
                    model = result;
                    return result.Lods.Count > 0;
                }

                Vector3[] vertices;
                if (!TryReadVerticesTag(reader, verticesOffset, out vertices))
                {
                    model = result;
                    return result.Lods.Count > 0;
                }

                Vector2[] uvs = null;
                if (tags.TryGetValue("TexCoords0", out texCoordsOffset))
                {
                    float patternScale = 1f;
                    if (tags.TryGetValue("PatternScale", out patternScaleOffset))
                        TryReadPatternScaleTag(reader, patternScaleOffset, out patternScale);
                    TryReadTexCoordsTag(reader, texCoordsOffset, patternScale, Vector2.Zero, out uvs);
                }

                ReadMeshPartsTag(reader, meshPartsOffset, result.Version, vertices, uvs, result, targetMaterial);
                model = result;
                return true;
            }
            catch
            {
                model = null;
                return false;
            }
        }

        private static int ReadVersion(string[] debugLines)
        {
            const string prefix = "Version:";
            if (debugLines == null) return 0;

            for (int i = 0; i < debugLines.Length; i++)
            {
                string line = debugLines[i];
                if (line == null || !line.StartsWith(prefix, StringComparison.Ordinal)) continue;

                int version;
                if (int.TryParse(line.Substring(prefix.Length), out version)) return version;
            }

            return 0;
        }

        private static string[] ReadStringArray(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            string[] result = new string[count];
            for (int i = 0; i < count; i++)
                result[i] = reader.ReadString();
            return result;
        }

        private static Dictionary<string, int> ReadIndexDictionary(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            Dictionary<string, int> result = new Dictionary<string, int>(count, StringComparer.Ordinal);
            for (int i = 0; i < count; i++)
                result[reader.ReadString()] = reader.ReadInt32();
            return result;
        }

        private static bool TryReadVerticesTag(BinaryReader reader, int offset, out Vector3[] vertices)
        {
            vertices = null;
            if (!SeekTag(reader, offset, "Vertices")) return false;

            int count = reader.ReadInt32();
            vertices = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                ulong packed = reader.ReadUInt64();
                vertices[i] = new Vector3(
                    HalfToFloat((ushort)(packed & 0xffff)),
                    HalfToFloat((ushort)((packed >> 16) & 0xffff)),
                    HalfToFloat((ushort)((packed >> 32) & 0xffff)));
            }

            return true;
        }

        private static void TryReadTexCoordsTag(BinaryReader reader, int offset, float patternScale, Vector2 offsetUv,
            out Vector2[] uvs)
        {
            uvs = null;
            if (!SeekTag(reader, offset, "TexCoords0")) return;

            if (Math.Abs(patternScale) < 1e-6f) patternScale = 1f;

            int count = reader.ReadInt32();
            uvs = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                uint packed = reader.ReadUInt32();
                Vector2 vector = new Vector2(
                    HalfToFloat((ushort)(packed & 0xffff)),
                    HalfToFloat((ushort)((packed >> 16) & 0xffff))) / patternScale + offsetUv;
                uvs[i] = new Vector2(vector.X, 0f - vector.Y);
            }
        }

        private static void TryReadLodsTag(BinaryReader reader, int offset, List<ScfMinimalMwmLod> lods)
        {
            if (lods == null || !SeekTag(reader, offset, "LODs")) return;

            int count = reader.ReadInt32();
            if (count < 0) return;

            for (int i = 0; i < count; i++)
            {
                float distance = reader.ReadSingle();
                string path = reader.ReadString();
                SkipOptionalNullTerminator(reader);

                if (!string.IsNullOrWhiteSpace(path))
                {
                    lods.Add(new ScfMinimalMwmLod
                    {
                        Distance = distance,
                        Path = path
                    });
                }
            }
        }

        private static void SkipOptionalNullTerminator(BinaryReader reader)
        {
            if (reader == null || !reader.BaseStream.CanSeek) return;

            Stream stream = reader.BaseStream;
            if (stream.Position >= stream.Length) return;

            long position = stream.Position;
            if (reader.ReadByte() != 0)
                stream.Position = position;
        }

        private static void TryReadPatternScaleTag(BinaryReader reader, int offset, out float patternScale)
        {
            patternScale = 1f;
            if (!SeekTag(reader, offset, "PatternScale")) return;

            patternScale = reader.ReadSingle();
            if (Math.Abs(patternScale) < 1e-6f) patternScale = 1f;
        }

        private static void ReadMeshPartsTag(BinaryReader reader, int offset, int version, Vector3[] vertices,
            Vector2[] uvs, ScfMinimalMwmModel model, string targetMaterial)
        {
            if (!SeekTag(reader, offset, "MeshParts")) return;

            int count = reader.ReadInt32();
            int globalTriangleOffset = 0;

            for (int i = 0; i < count; i++)
            {
                reader.ReadInt32();
                if (version < 1052001) reader.ReadInt32();

                int[] indices = ReadIntArray(reader);
                int partTriangleCount = indices != null ? indices.Length / 3 : 0;

                string material = null;
                if (reader.ReadBoolean())
                    material = ReadMaterialDescriptor(reader, version);

                if (!string.IsNullOrWhiteSpace(material) &&
                    (string.IsNullOrWhiteSpace(targetMaterial) ||
                     string.Equals(material, targetMaterial, StringComparison.OrdinalIgnoreCase)))
                {
                    ScfMinimalMwmScreenAreaGeometry area;
                    if (!model.AreasByMaterial.TryGetValue(material, out area))
                    {
                        area = new ScfMinimalMwmScreenAreaGeometry
                        {
                            Material = material
                        };
                        model.AreasByMaterial[material] = area;
                    }

                    area.AddTriangles(vertices, uvs, indices, globalTriangleOffset);
                }

                globalTriangleOffset += partTriangleCount;
            }
        }

        private static int[] ReadIntArray(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            int[] result = new int[count];
            for (int i = 0; i < count; i++)
                result[i] = reader.ReadInt32();
            return result;
        }

        private static string ReadMaterialDescriptor(BinaryReader reader, int version)
        {
            string materialName = reader.ReadString();
            if (version < 1052002)
            {
                reader.ReadString();
                reader.ReadString();
            }
            else
            {
                SkipStringDictionary(reader);
            }

            if (version >= 1068001) SkipStringDictionary(reader);

            if (version < 1157001)
            {
                for (int i = 0; i < 7; i++)
                    reader.ReadSingle();
            }

            string technique = version < 1052001 ? reader.ReadInt32().ToString() : reader.ReadString();
            if (technique == "GLASS")
            {
                if (version >= 1043001)
                {
                    reader.ReadString();
                    reader.ReadString();
                    reader.ReadBoolean();
                }
                else
                {
                    for (int i = 0; i < 4; i++)
                        reader.ReadSingle();
                }
            }

            return materialName;
        }

        private static void SkipStringDictionary(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                reader.ReadString();
                reader.ReadString();
            }
        }

        private static bool SeekTag(BinaryReader reader, int offset, string expectedTag)
        {
            reader.BaseStream.Position = offset;
            string tag = reader.ReadString();
            return string.Equals(tag, expectedTag, StringComparison.Ordinal);
        }

        private static float HalfToFloat(ushort value)
        {
            float sign = (value & 0x8000) == 0 ? 1f : -1f;
            int exponent = (value >> 10) & 0x1f;
            int mantissa = value & 0x03ff;

            if (exponent == 0)
            {
                if (mantissa == 0) return sign * 0f;
                return sign * (float)(Math.Pow(2d, -14d) * (mantissa / 1024d));
            }

            if (exponent == 31)
                return sign > 0f ? float.PositiveInfinity : float.NegativeInfinity;

            return sign * (float)(Math.Pow(2d, exponent - 15d) * (1d + mantissa / 1024d));
        }
    }
}
