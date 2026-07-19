using System;
using System.Collections.Generic;
using System.IO;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using IngameTextSurface = Sandbox.ModAPI.Ingame.IMyTextSurface;

namespace ShipCoreFramework
{
    internal static class ScfScreenAreaGeometry
    {
        private static readonly Dictionary<string, CachedScreenArea> Cache =
            new Dictionary<string, CachedScreenArea>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, Matrix> LocalMatrixCache =
            new Dictionary<string, Matrix>(StringComparer.OrdinalIgnoreCase);

        internal static bool TryGetScreenPointIntersection(
            IMyCubeBlock block,
            int surfaceIndex,
            IngameTextSurface surface,
            Vector3D rayOrigin,
            Vector3D rayDirection,
            out Vector2 screenPoint)
        {
            screenPoint = Vector2.Zero;
            if (block == null || surface == null) return false;
            if (rayDirection.LengthSquared() <= 1e-8) return false;

            rayDirection.Normalize();

            Vector2 uv;
            if (!TryGetScreenUvIntersection(block, surfaceIndex, rayOrigin, rayDirection, out uv))
                return false;

            screenPoint = ToSurfacePoint(surface, uv);
            return true;
        }

        internal static bool TryGetScreenUvIntersection(
            IMyCubeBlock block,
            int surfaceIndex,
            Vector3D rayOrigin,
            Vector3D rayDirection,
            out Vector2 uv)
        {
            uv = Vector2.Zero;
            if (block == null) return false;

            var blockEntity = block as IMyEntity;
            if (blockEntity?.Model == null) return false;

            ScfMinimalMwmScreenAreaGeometry geometry;
            if (!TryGetScreenAreaGeometry(block, surfaceIndex, out geometry))
                return false;

            return TryGetScreenUvIntersection(blockEntity.Model, block.WorldMatrix, geometry, rayOrigin, rayDirection, out uv);
        }

        internal static bool TryGetScreenWorldMatrix(IMyCubeBlock block, int surfaceIndex, out MatrixD worldMatrix)
        {
            worldMatrix = MatrixD.Identity;
            if (block == null) return false;

            Matrix localMatrix;
            if (!TryGetScreenLocalMatrix(block, surfaceIndex, out localMatrix))
                return false;

            worldMatrix = (MatrixD)localMatrix * block.WorldMatrix;
            return true;
        }

        internal static bool TryGetScreenAreaSize(IMyCubeBlock block, int surfaceIndex, out double widthM, out double heightM)
        {
            widthM = 0d;
            heightM = 0d;

            ScfMinimalMwmScreenAreaGeometry geometry;
            if (!TryGetScreenAreaGeometry(block, surfaceIndex, out geometry))
                return false;

            widthM = geometry.WidthM;
            heightM = geometry.HeightM;
            return widthM > 0.01d && heightM > 0.01d;
        }

        internal static bool TryGetScreenLocalMatrix(IMyCubeBlock block, int surfaceIndex, out Matrix localMatrix)
        {
            localMatrix = Matrix.Identity;
            if (block == null) return false;

            var localKey = BuildLocalMatrixCacheKey(block, surfaceIndex);
            if (!string.IsNullOrEmpty(localKey) && LocalMatrixCache.TryGetValue(localKey, out localMatrix))
                return true;

            ScfMinimalMwmScreenAreaGeometry geometry;
            if (!TryGetScreenAreaGeometry(block, surfaceIndex, out geometry))
                return false;

            var blockEntity = block as IMyEntity;
            if (blockEntity?.Model == null)
                return false;

            if (!TryGetScreenLocalMatrix(blockEntity.Model, geometry, out localMatrix))
                return false;

            if (!string.IsNullOrEmpty(localKey))
                LocalMatrixCache[localKey] = localMatrix;

            return true;
        }

        private static Vector2 ToSurfacePoint(IngameTextSurface surface, Vector2 uv)
        {
            if (surface == null) return Vector2.Zero;

            var offset = (surface.TextureSize - surface.SurfaceSize) * 0.5f;
            return new Vector2(
                offset.X + uv.X * surface.SurfaceSize.X,
                offset.Y + uv.Y * surface.SurfaceSize.Y);
        }

        private static bool TryGetScreenLocalMatrix(IMyModel blockModel,
            ScfMinimalMwmScreenAreaGeometry geometry,
            out Matrix localMatrix)
        {
            localMatrix = Matrix.Identity;
            if (blockModel == null || geometry?.TriangleIndices == null ||
                geometry.UvTriangles == null)
                return false;

            int bestTriangleIndex;
            if (!TryGetCenterTriangleIndex(geometry, out bestTriangleIndex))
                return false;

            Vector3 centerPoint;
            Vector3 right;
            Vector3 upFromUv;
            Vector3 forward;
            if (!TryGetScreenLocalAxesFromTriangleUv(blockModel, geometry, bestTriangleIndex, out right, out upFromUv,
                    out forward))
                return false;

            var rawCenter = GetScreenCenterRawUv(geometry);
            if (!TryGetLocalPointAtRawUv(blockModel, geometry, rawCenter, out centerPoint))
            {
                var triangle = blockModel.GetTriangle(geometry.TriangleIndices[bestTriangleIndex]);
                Vector3 aLocal;
                Vector3 bLocal;
                Vector3 cLocal;
                blockModel.GetVertex(triangle.I0, triangle.I1, triangle.I2, out aLocal, out bLocal, out cLocal);
                centerPoint = (aLocal + bLocal + cLocal) / 3f;
            }

            localMatrix = Matrix.Identity;
            localMatrix.Forward = forward;
            localMatrix.Right = right;
            localMatrix.Up = upFromUv;
            localMatrix.Translation = centerPoint;
            return true;
        }

        private static bool TryGetScreenLocalAxesFromTriangleUv(
            IMyModel blockModel,
            ScfMinimalMwmScreenAreaGeometry geometry,
            int geometryTriangleIndex,
            out Vector3 right,
            out Vector3 up,
            out Vector3 forward)
        {
            right = Vector3.Zero;
            up = Vector3.Zero;
            forward = Vector3.Zero;
            if (blockModel == null || geometry?.TriangleIndices == null || geometry.UvTriangles == null)
                return false;

            if (geometryTriangleIndex < 0 || geometryTriangleIndex >= geometry.TriangleIndices.Count ||
                geometryTriangleIndex >= geometry.UvTriangles.Count)
                return false;

            var modelTriangle = blockModel.GetTriangle(geometry.TriangleIndices[geometryTriangleIndex]);
            Vector3 p0;
            Vector3 p1;
            Vector3 p2;
            blockModel.GetVertex(modelTriangle.I0, modelTriangle.I1, modelTriangle.I2, out p0, out p1, out p2);

            Vector2 uv0;
            Vector2 uv1;
            Vector2 uv2;
            if (!TryGetRuntimeTriangleUvs(geometry, modelTriangle.I0, modelTriangle.I1, modelTriangle.I2, out uv0,
                    out uv1, out uv2))
            {
                var fallback = geometry.UvTriangles[geometryTriangleIndex];
                uv0 = fallback.A;
                uv1 = fallback.B;
                uv2 = fallback.C;
            }

            var dp1 = p1 - p0;
            var dp2 = p2 - p0;
            var duv1 = uv1 - uv0;
            var duv2 = uv2 - uv0;
            var det = duv1.X * duv2.Y - duv1.Y * duv2.X;
            if (Math.Abs(det) <= 1e-10f) return false;

            var invDet = 1f / det;
            var dPdu = (dp1 * duv2.Y - dp2 * duv1.Y) * invDet;
            var dPdv = (dp2 * duv1.X - dp1 * duv2.X) * invDet;

            right = dPdu;
            if (right.LengthSquared() <= 1e-12f) return false;
            right.Normalize();

            up = -dPdv;
            up -= right * Vector3.Dot(up, right);
            if (up.LengthSquared() <= 1e-12f) return false;
            up.Normalize();

            forward = Vector3.Cross(right, up);
            if (forward.LengthSquared() <= 1e-12f) return false;
            forward.Normalize();

            up = Vector3.Cross(right, forward);
            if (up.LengthSquared() <= 1e-12f) return false;
            up.Normalize();

            return true;
        }

        private static Vector2 GetScreenCenterRawUv(ScfMinimalMwmScreenAreaGeometry geometry)
        {
            if (geometry != null && geometry.HasUvBounds)
                return (geometry.UvMin + geometry.UvMax) * 0.5f;

            return new Vector2(0.5f, -0.5f);
        }

        private static bool TryGetCenterTriangleIndex(ScfMinimalMwmScreenAreaGeometry geometry, out int triangleIndex)
        {
            triangleIndex = -1;
            if (geometry?.TriangleIndices == null || geometry.UvTriangles == null)
                return false;

            var centerRawUv = GetScreenCenterRawUv(geometry);
            var count = Math.Min(geometry.TriangleIndices.Count, geometry.UvTriangles.Count);
            var bestDistanceSq = double.MaxValue;
            for (var i = 0; i < count; i++)
            {
                var uvTriangle = geometry.UvTriangles[i];
                float u;
                float v;
                float w;
                if (TryGetUvBarycentric(centerRawUv, uvTriangle.A, uvTriangle.B, uvTriangle.C, out u, out v, out w))
                {
                    triangleIndex = i;
                    return true;
                }

                var distanceSq = DistanceSquaredToUvTriangle(centerRawUv, uvTriangle);
                if (distanceSq >= bestDistanceSq) continue;

                bestDistanceSq = distanceSq;
                triangleIndex = i;
            }

            return triangleIndex >= 0;
        }

        private static bool TryGetLocalPointAtRawUv(IMyModel blockModel,
            ScfMinimalMwmScreenAreaGeometry geometry,
            Vector2 rawUv,
            out Vector3 point)
        {
            point = Vector3.Zero;
            if (blockModel == null || geometry?.TriangleIndices == null ||
                geometry.UvTriangles == null)
                return false;

            var count = Math.Min(geometry.TriangleIndices.Count, geometry.UvTriangles.Count);
            var bestTriangleIndex = -1;
            var bestDistanceSq = double.MaxValue;
            var bestU = 0f;
            var bestV = 0f;
            var bestW = 0f;

            for (var i = 0; i < count; i++)
            {
                var triangle = blockModel.GetTriangle(geometry.TriangleIndices[i]);
                Vector2 uv0;
                Vector2 uv1;
                Vector2 uv2;
                if (!TryGetRuntimeTriangleUvs(geometry, triangle.I0, triangle.I1, triangle.I2, out uv0, out uv1, out uv2))
                {
                    var fallback = geometry.UvTriangles[i];
                    uv0 = fallback.A;
                    uv1 = fallback.B;
                    uv2 = fallback.C;
                }

                float u;
                float v;
                float w;
                if (TryGetUvBarycentric(rawUv, uv0, uv1, uv2, out u, out v, out w))
                {
                    bestTriangleIndex = i;
                    bestU = u;
                    bestV = v;
                    bestW = w;
                    break;
                }

                var distanceSq = DistanceSquaredToUvTriangle(rawUv, new ScfMinimalMwmUvTriangle(uv0, uv1, uv2));
                if (distanceSq >= bestDistanceSq) continue;

                bestDistanceSq = distanceSq;
                bestTriangleIndex = i;
                GetClosestUvBarycentric(rawUv, uv0, uv1, uv2, out bestU, out bestV, out bestW);
            }

            if (bestTriangleIndex < 0) return false;

            var bestTriangle = blockModel.GetTriangle(geometry.TriangleIndices[bestTriangleIndex]);
            Vector3 aLocal;
            Vector3 bLocal;
            Vector3 cLocal;
            blockModel.GetVertex(bestTriangle.I0, bestTriangle.I1, bestTriangle.I2, out aLocal, out bLocal, out cLocal);
            point = aLocal * bestU + bLocal * bestV + cLocal * bestW;
            return true;
        }

        private static void GetClosestUvBarycentric(Vector2 point, Vector2 a, Vector2 b, Vector2 c,
            out float u, out float v, out float w)
        {
            var abPoint = ClosestPointOnUvSegment(point, a, b);
            var bcPoint = ClosestPointOnUvSegment(point, b, c);
            var caPoint = ClosestPointOnUvSegment(point, c, a);
            var abDistance = Vector2.DistanceSquared(point, abPoint);
            var bcDistance = Vector2.DistanceSquared(point, bcPoint);
            var caDistance = Vector2.DistanceSquared(point, caPoint);

            if (abDistance <= bcDistance && abDistance <= caDistance)
            {
                var t = GetUvSegmentT(abPoint, a, b);
                u = 1f - t;
                v = t;
                w = 0f;
                return;
            }

            if (bcDistance <= caDistance)
            {
                var t = GetUvSegmentT(bcPoint, b, c);
                u = 0f;
                v = 1f - t;
                w = t;
                return;
            }

            var caT = GetUvSegmentT(caPoint, c, a);
            u = caT;
            v = 0f;
            w = 1f - caT;
        }

        private static Vector2 ClosestPointOnUvSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var segment = b - a;
            var lengthSq = segment.LengthSquared();
            if (lengthSq <= 1e-12f) return a;

            var t = MathHelper.Clamp(Vector2.Dot(point - a, segment) / lengthSq, 0f, 1f);
            return a + segment * t;
        }

        private static float GetUvSegmentT(Vector2 point, Vector2 a, Vector2 b)
        {
            var segment = b - a;
            var lengthSq = segment.LengthSquared();
            if (lengthSq <= 1e-12f) return 0f;

            return MathHelper.Clamp(Vector2.Dot(point - a, segment) / lengthSq, 0f, 1f);
        }

        private static bool TryGetRuntimeTriangleUvs(ScfMinimalMwmScreenAreaGeometry geometry, int i0, int i1, int i2,
            out Vector2 uv0, out Vector2 uv1, out Vector2 uv2)
        {
            uv0 = Vector2.Zero;
            uv1 = Vector2.Zero;
            uv2 = Vector2.Zero;

            if (geometry?.UvByVertexIndex == null) return false;

            return geometry.UvByVertexIndex.TryGetValue(i0, out uv0) &&
                   geometry.UvByVertexIndex.TryGetValue(i1, out uv1) &&
                   geometry.UvByVertexIndex.TryGetValue(i2, out uv2);
        }

        private static bool TryGetScreenAreaGeometry(IMyCubeBlock block, int surfaceIndex,
            out ScfMinimalMwmScreenAreaGeometry geometry)
        {
            geometry = null;
            if (block == null) return false;

            var blockEntity = block as IMyEntity;
            if (blockEntity?.Model == null) return false;

            var assetName = blockEntity.Model.AssetName;
            if (string.IsNullOrWhiteSpace(assetName)) return false;

            var materials = ResolveMaterialCandidates(block, surfaceIndex);
            if (materials.Count == 0) return false;

            for (var i = 0; i < materials.Count; i++)
            {
                if (TryGetScreenAreaGeometry(assetName, materials[i], out geometry))
                    return true;
            }

            return false;
        }

        private static List<string> ResolveMaterialCandidates(IMyCubeBlock block, int surfaceIndex)
        {
            var result = new List<string>();
            var definition = block?.SlimBlock.BlockDefinition as MyFunctionalBlockDefinition;

            if (definition?.ScreenAreas == null || definition.ScreenAreas.Count == 0)
                return result;

            if (surfaceIndex < 0 || surfaceIndex >= definition.ScreenAreas.Count)
                return result;

            AddMaterialCandidate(result, definition.ScreenAreas[surfaceIndex].Name);
            return result;
        }

        private static void AddMaterialCandidate(List<string> materials, string material)
        {
            if (materials == null || string.IsNullOrWhiteSpace(material)) return;

            for (var i = 0; i < materials.Count; i++)
            {
                if (string.Equals(materials[i], material, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            materials.Add(material);
        }

        private static string BuildLocalMatrixCacheKey(IMyCubeBlock block, int surfaceIndex)
        {
            if (block == null || block.BlockDefinition.TypeId.IsNull) return null;
            return block.BlockDefinition.TypeIdString + "/" + block.BlockDefinition.SubtypeName + "#" + surfaceIndex;
        }

        private static bool TryGetScreenAreaGeometry(string assetName, string material,
            out ScfMinimalMwmScreenAreaGeometry geometry)
        {
            geometry = null;
            if (string.IsNullOrWhiteSpace(assetName) || string.IsNullOrWhiteSpace(material)) return false;

            var cacheKey = assetName + "|" + material;
            CachedScreenArea cached;
            if (Cache.TryGetValue(cacheKey, out cached))
            {
                geometry = cached.Geometry;
                return geometry != null && geometry.TriangleCount > 0;
            }

            cached = new CachedScreenArea();
            Cache[cacheKey] = cached;

            var modelPath = ToModelPath(assetName);
            if (string.IsNullOrWhiteSpace(modelPath))
            {
                cached.LoadError = "could not normalize asset name to content path";
                return false;
            }

            var lodPath = modelPath;
            try
            {
                using (var model = OpenMwm(modelPath))
                {
                    List<string> lods;
                    if (model != null && ScfMinimalMwmReader.TryReadLodPaths(model, out lods) && lods.Count > 0)
                    {
                        var candidate = lods[0];
                        if (!candidate.EndsWith(".mwm", StringComparison.OrdinalIgnoreCase))
                            candidate += ".mwm";
                        var normalized = ToModelPath(candidate);
                        if (!string.IsNullOrWhiteSpace(normalized))
                            lodPath = normalized;
                    }
                }
            }
            catch
            {
                lodPath = modelPath;
            }

            using (var reader = OpenMwm(lodPath) ?? OpenMwm(modelPath))
            {
                if (reader == null)
                {
                    cached.LoadError = "MWM file not found: " + assetName;
                    return false;
                }

                ScfMinimalMwmScreenAreaGeometry parsed;
                if (!ScfMinimalMwmReader.TryReadScreenArea(reader, material, out parsed))
                {
                    cached.LoadError = "material not found or had no triangles: " + material;
                    return false;
                }

                cached.Geometry = parsed;
                cached.LoadError = null;
                geometry = parsed;
                return true;
            }
        }

        private static BinaryReader OpenMwm(string content)
        {
            if (string.IsNullOrWhiteSpace(content) || MyAPIGateway.Utilities == null) return null;

            var candidates = BuildContentPathCandidates(content);
            if (MyAPIGateway.Session != null && MyAPIGateway.Session.Mods != null)
            {
                for (var i = 0; i < candidates.Count; i++)
                {
                    foreach (var mod in MyAPIGateway.Session.Mods)
                    {
                        var reader = TryOpenModMwm(candidates[i], mod);
                        if (reader != null) return reader;
                    }
                }
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                try
                {
                    var reader = MyAPIGateway.Utilities.ReadBinaryFileInGameContent(candidate);
                    if (reader != null) return reader;
                }
                catch
                {
                }

                try
                {
                    if (MyAPIGateway.Utilities.FileExistsInGameContent(candidate))
                        return MyAPIGateway.Utilities.ReadBinaryFileInGameContent(candidate);
                }
                catch
                {
                }
            }

            return null;
        }

        private static BinaryReader TryOpenModMwm(string content, MyObjectBuilder_Checkpoint.ModItem mod)
        {
            try
            {
                var reader = MyAPIGateway.Utilities.ReadBinaryFileInModLocation(content, mod);
                if (reader != null) return reader;
            }
            catch
            {
            }

            try
            {
                if (MyAPIGateway.Utilities.FileExistsInModLocation(content, mod))
                    return MyAPIGateway.Utilities.ReadBinaryFileInModLocation(content, mod);
            }
            catch
            {
            }

            return null;
        }

        private static List<string> BuildContentPathCandidates(string content)
        {
            var candidates = new List<string>();
            var forward = content.Replace('\\', '/');
            AddContentPathCandidate(candidates, content);
            AddContentPathCandidate(candidates, forward);
            AddContentPathCandidate(candidates, content.Replace('/', '\\'));

            while (forward.StartsWith("/", StringComparison.Ordinal))
                forward = forward.Substring(1);

            if (forward.StartsWith("data/", StringComparison.OrdinalIgnoreCase) && forward.Length > 5)
            {
                AddContentPathCandidate(candidates, "Data/" + forward.Substring(5));
                AddContentPathCandidate(candidates, "data/" + forward.Substring(5));
            }

            return candidates;
        }

        private static void AddContentPathCandidate(List<string> candidates, string content)
        {
            if (candidates == null || string.IsNullOrWhiteSpace(content)) return;

            var normalized = content.Trim();
            while (normalized.StartsWith("/", StringComparison.Ordinal) ||
                   normalized.StartsWith("\\", StringComparison.Ordinal))
                normalized = normalized.Substring(1);

            for (var i = 0; i < candidates.Count; i++)
                if (string.Equals(candidates[i], normalized, StringComparison.Ordinal))
                    return;

            candidates.Add(normalized);
        }

        private static string ToModelPath(string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName)) return null;

            var path = assetName.Replace('\\', '/');
            const string contentMarker = "/Content/";

            var contentIndex = path.IndexOf(contentMarker, StringComparison.OrdinalIgnoreCase);
            if (contentIndex >= 0)
                path = path.Substring(contentIndex + contentMarker.Length);

            while (path.StartsWith("/", StringComparison.Ordinal))
                path = path.Substring(1);

            if (path.StartsWith("244850/", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring("244850/".Length);
                var slash = path.IndexOf("/", StringComparison.Ordinal);
                if (slash >= 0 && slash + 1 < path.Length)
                    path = path.Substring(slash + 1);
            }

            if (!path.EndsWith(".mwm", StringComparison.OrdinalIgnoreCase))
                return null;

            var extensionIndex = path.LastIndexOf(".mwm", StringComparison.OrdinalIgnoreCase);
            if (extensionIndex < 0) return null;

            return path.Substring(0, extensionIndex) + path.Substring(extensionIndex);
        }

        private static bool TryGetScreenUvIntersection(IMyModel blockModel,
            MatrixD worldMatrix,
            ScfMinimalMwmScreenAreaGeometry geometry,
            Vector3D rayOrigin,
            Vector3D rayDirection,
            out Vector2 uv)
        {
            double distance;
            Vector2 rawUv;
            return TryGetScreenIntersection(blockModel, worldMatrix, geometry, rayOrigin, rayDirection, double.MaxValue,
                out uv, out rawUv, out distance);
        }

        private static bool TryGetScreenIntersection(IMyModel blockModel,
            MatrixD worldMatrix,
            ScfMinimalMwmScreenAreaGeometry geometry,
            Vector3D rayOrigin,
            Vector3D rayDirection,
            double maxDistance,
            out Vector2 uv,
            out Vector2 rawUv,
            out double hitDistance)
        {
            uv = Vector2.Zero;
            rawUv = Vector2.Zero;
            hitDistance = 0d;
            if (blockModel == null || geometry?.TriangleIndices == null ||
                geometry.UvTriangles == null)
                return false;

            var bestDistance = double.MaxValue;
            var bestRawUv = Vector2.Zero;
            var hit = false;
            var count = Math.Min(geometry.TriangleIndices.Count, geometry.UvTriangles.Count);
            for (var i = 0; i < count; i++)
            {
                var triangleIndex = geometry.TriangleIndices[i];
                var t = blockModel.GetTriangle(triangleIndex);

                Vector3 aLocal;
                Vector3 bLocal;
                Vector3 cLocal;
                blockModel.GetVertex(t.I0, t.I1, t.I2, out aLocal, out bLocal, out cLocal);

                var a = Vector3D.Transform((Vector3D)aLocal, worldMatrix);
                var b = Vector3D.Transform((Vector3D)bLocal, worldMatrix);
                var c = Vector3D.Transform((Vector3D)cLocal, worldMatrix);

                var normal = Vector3D.Cross(b - a, c - a);
                if (Vector3D.Dot(normal, rayDirection) >= -1e-9) continue;

                double distance;
                double u;
                double v;
                if (!TryIntersectTriangle(rayOrigin, rayDirection, a, b, c, out distance, out u, out v))
                    continue;
                if (distance > maxDistance || distance >= bestDistance)
                    continue;

                var w = 1d - u - v;
                if (!TryGetRuntimeTriangleUv(geometry, t.I0, t.I1, t.I2, w, u, v, out bestRawUv))
                {
                    var uvTriangle = geometry.UvTriangles[i];
                    bestRawUv = uvTriangle.A * (float)w + uvTriangle.B * (float)u + uvTriangle.C * (float)v;
                }

                bestDistance = distance;
                hit = true;
            }

            if (!hit) return false;

            rawUv = bestRawUv;
            uv = ToScreenUv(rawUv);
            hitDistance = bestDistance;
            return true;
        }

        private static bool TryGetRuntimeTriangleUv(ScfMinimalMwmScreenAreaGeometry geometry, int i0, int i1, int i2,
            double w, double u, double v, out Vector2 uv)
        {
            uv = Vector2.Zero;
            if (geometry?.UvByVertexIndex == null) return false;

            Vector2 uv0;
            Vector2 uv1;
            Vector2 uv2;
            if (!geometry.UvByVertexIndex.TryGetValue(i0, out uv0) ||
                !geometry.UvByVertexIndex.TryGetValue(i1, out uv1) ||
                !geometry.UvByVertexIndex.TryGetValue(i2, out uv2))
                return false;

            uv = uv0 * (float)w + uv1 * (float)u + uv2 * (float)v;
            return true;
        }

        private static Vector2 ToScreenUv(Vector2 rawUv)
        {
            return new Vector2(rawUv.X, -rawUv.Y);
        }

        private static bool TryIntersectTriangle(Vector3D rayOrigin, Vector3D rayDirection, Vector3D a, Vector3D b,
            Vector3D c, out double distance, out double u, out double v)
        {
            distance = 0d;
            u = 0d;
            v = 0d;

            var edge1 = b - a;
            var edge2 = c - a;
            var p = Vector3D.Cross(rayDirection, edge2);
            var det = Vector3D.Dot(edge1, p);
            if (Math.Abs(det) < 1e-9) return false;

            var invDet = 1d / det;
            var t = rayOrigin - a;
            u = Vector3D.Dot(t, p) * invDet;
            if (u < 0d || u > 1d) return false;

            var q = Vector3D.Cross(t, edge1);
            v = Vector3D.Dot(rayDirection, q) * invDet;
            if (v < 0d || u + v > 1d) return false;

            distance = Vector3D.Dot(edge2, q) * invDet;
            return distance > 0d;
        }

        private static bool TryGetUvBarycentric(Vector2 point, Vector2 a, Vector2 b, Vector2 c,
            out float u, out float v, out float w)
        {
            const float epsilon = 1e-5f;
            u = 0f;
            v = 0f;
            w = 0f;

            var v0 = b - a;
            var v1 = c - a;
            var v2 = point - a;
            var d00 = Vector2.Dot(v0, v0);
            var d01 = Vector2.Dot(v0, v1);
            var d11 = Vector2.Dot(v1, v1);
            var d20 = Vector2.Dot(v2, v0);
            var d21 = Vector2.Dot(v2, v1);
            var denom = d00 * d11 - d01 * d01;
            if (Math.Abs(denom) <= epsilon) return false;

            v = (d11 * d20 - d01 * d21) / denom;
            w = (d00 * d21 - d01 * d20) / denom;
            u = 1f - v - w;
            return u >= -epsilon && v >= -epsilon && w >= -epsilon;
        }

        private static double DistanceSquaredToUvTriangle(Vector2 point, ScfMinimalMwmUvTriangle triangle)
        {
            float u;
            float v;
            float w;
            if (TryGetUvBarycentric(point, triangle.A, triangle.B, triangle.C, out u, out v, out w))
                return 0d;

            return Math.Min(
                DistanceSquaredToUvSegment(point, triangle.A, triangle.B),
                Math.Min(
                    DistanceSquaredToUvSegment(point, triangle.B, triangle.C),
                    DistanceSquaredToUvSegment(point, triangle.C, triangle.A)));
        }

        private static double DistanceSquaredToUvSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var segment = b - a;
            var lengthSq = segment.LengthSquared();
            if (lengthSq <= 1e-12f) return Vector2.DistanceSquared(point, a);

            var t = MathHelper.Clamp(Vector2.Dot(point - a, segment) / lengthSq, 0f, 1f);
            var closest = a + segment * t;
            return Vector2.DistanceSquared(point, closest);
        }

        private sealed class CachedScreenArea
        {
            internal ScfMinimalMwmScreenAreaGeometry Geometry;
            internal string LoadError;
        }
    }
}
