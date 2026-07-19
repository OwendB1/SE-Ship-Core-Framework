using System;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game;
using VRageMath;

namespace ShipCoreFramework
{
    public partial class Session
    {
        private const int HighResolutionLcdTextureSize = 1024;
        private static readonly string[] HighResolutionLcdSubtypes =
        {
            "LargeLCDPanel3x3", "LargeLCDPanel5x3", "LargeLCDPanel5x5"
        };
        private static readonly List<LcdDefinitionTextureState> OriginalLcdTextureStates =
            new List<LcdDefinitionTextureState>();
        public override void Draw()
        {
            if (!IsClient) return;
            var camPos = MyAPIGateway.Session.Camera.WorldMatrix.Translation;
            const double edgeProximity = 20000.0;

            var zones = Config.NoFlyZones;
            if (zones == null || zones.Count == 0) return;

            foreach (var z in zones)
            {
                var dist = Vector3D.Distance(z.Position, camPos);
                if (Math.Abs(dist - z.Radius) > edgeProximity) continue;

                var world = MatrixD.CreateTranslation(z.Position);
                var color = z.ForceOff ? new Color(1f, 0.2f, 0.2f, 0.05f) : new Color(0.2f, 0.6f, 1f, 0.05f);
                var edge = z.ForceOff ? new Color(1f, 0.1f, 0.1f, 1f) : new Color(0.3f, 0.7f, 1f, 1f);

                MySimpleObjectDraw.DrawTransparentSphere(ref world, (float)z.Radius, ref color, MySimpleObjectRasterizer.Solid,24, MatSphere, null, 1f);
                MySimpleObjectDraw.DrawTransparentSphere(ref world, (float)z.Radius, ref edge, MySimpleObjectRasterizer.Wireframe,64, null, MatLine, 0.75f);
            }

        }
        private static void ApplyHighResolutionLcdDefinitions()
        {
            if (OriginalLcdTextureStates.Count > 0 || MyDefinitionManager.Static == null)
                return;

            for (var subtypeIndex = 0; subtypeIndex < HighResolutionLcdSubtypes.Length; subtypeIndex++)
            {
                MyCubeBlockDefinition cubeDefinition;
                try
                {
                    var id = new MyDefinitionId(typeof(MyObjectBuilder_TextPanel),
                        HighResolutionLcdSubtypes[subtypeIndex]);
                    if (!MyDefinitionManager.Static.TryGetCubeBlockDefinition(id, out cubeDefinition))
                        continue;
                }
                catch (Exception e)
                {
                    Utils.Log("LCD definition lookup failed for " + HighResolutionLcdSubtypes[subtypeIndex] +
                              ": " + e.Message, 1);
                    continue;
                }

                var definition = cubeDefinition as MyTextPanelDefinition;
                if (definition?.ScreenAreas == null || definition.ScreenAreas.Count == 0)
                    continue;

                var state = new LcdDefinitionTextureState(definition);
                OriginalLcdTextureStates.Add(state);
                definition.TextureResolution = Math.Max(definition.TextureResolution, HighResolutionLcdTextureSize);

                for (var i = 0; i < definition.ScreenAreas.Count; i++)
                {
                    var area = definition.ScreenAreas[i];
                    if (area != null)
                        area.TextureResolution = Math.Max(area.TextureResolution, HighResolutionLcdTextureSize);
                }
            }
        }

        private static void RevertHighResolutionLcdDefinitions()
        {
            for (var stateIndex = 0; stateIndex < OriginalLcdTextureStates.Count; stateIndex++)
            {
                var state = OriginalLcdTextureStates[stateIndex];
                var definition = state.Definition;
                if (definition == null)
                    continue;

                if (definition.TextureResolution == HighResolutionLcdTextureSize)
                    definition.TextureResolution = state.TextureResolution;

                if (definition.ScreenAreas != null)
                {
                    var count = Math.Min(definition.ScreenAreas.Count, state.ScreenAreaTextureResolutions.Length);
                    for (var i = 0; i < count; i++)
                    {
                        var area = definition.ScreenAreas[i];
                        if (area != null && area.TextureResolution == HighResolutionLcdTextureSize)
                            area.TextureResolution = state.ScreenAreaTextureResolutions[i];
                    }
                }
            }

            OriginalLcdTextureStates.Clear();
        }

        private sealed class LcdDefinitionTextureState
        {
            internal readonly MyTextPanelDefinition Definition;
            internal readonly int TextureResolution;
            internal readonly int[] ScreenAreaTextureResolutions;

            internal LcdDefinitionTextureState(MyTextPanelDefinition definition)
            {
                Definition = definition;
                TextureResolution = definition.TextureResolution;
                ScreenAreaTextureResolutions = new int[definition.ScreenAreas.Count];
                for (var i = 0; i < definition.ScreenAreas.Count; i++)
                {
                    var area = definition.ScreenAreas[i];
                    ScreenAreaTextureResolutions[i] = area?.TextureResolution ?? 0;
                }
            }
        }
    }
}
