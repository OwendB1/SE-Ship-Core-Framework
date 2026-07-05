using System;
using System.Collections.Generic;
using System.Text;
using Draygo.API;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;

namespace ShipCoreFramework
{
    /// <summary>
    /// Client-only build-preview HUD. While the local player holds a block in build mode
    /// and aims at a tracked grid, it evaluates the proposed placement against the grid's
    /// limits (via the shared <see cref="LimitEvaluation"/>) and shows a capacity panel.
    ///
    /// The panel is screen-space text (crisp, no clipping) but anchored to the previewed
    /// block: each tick the block's world position is projected to screen coordinates and
    /// used as the panel origin, so it tracks the block while staying legible.
    ///
    /// Phase 2: capacity (block-limit buckets + hard caps) only. Directional feedback is
    /// added in a later phase. Renders through Text HUD API when present, falling back to a
    /// HUD notification otherwise.
    /// </summary>
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class BuildPreviewHud : MySessionComponentBase
    {
        private const int RecomputeIntervalTicks = 6; // ~10x/second
        private const double ScreenGapX = 0.015d;      // horizontal gap between block edge and text
        private const string PassColor = "120,220,120";
        private const string FailColor = "255,90,90";
        private const string TitleColor = "180,200,255";

        private bool _isClient;
        private HudAPIv2 _hudApi;
        private bool _hudReady;
        private HudAPIv2.HUDMessage _panel;
        private IMyHudNotification _fallback;

        private int _tick;
        private int _nextRecomputeTick;
        private long _lastGridId;
        private string _lastBlockId = string.Empty;
        private List<LimitCheckResult> _results;

        private Vector3D _boxCenter;
        private readonly Vector3D[] _corners = new Vector3D[8];

        private readonly StringBuilder _sb = new StringBuilder();

        public override void LoadData()
        {
            _isClient = !MyAPIGateway.Utilities.IsDedicated;
            if (!_isClient) return;

            _hudApi = new HudAPIv2(OnHudReady);
        }

        private void OnHudReady()
        {
            _hudReady = true;
            _panel = new HudAPIv2.HUDMessage(new StringBuilder(), Vector2D.Zero)
            {
                Visible = false
            };
        }

        protected override void UnloadData()
        {
            try
            {
                _hudApi?.Unload();
            }
            catch (Exception ex)
            {
                Utils.Log($"BuildPreviewHud.UnloadData: {ex}", 3);
            }

            _hudApi = null;
            _panel = null;
            _fallback = null;
        }

        public override void UpdateAfterSimulation()
        {
            if (!_isClient) return;

            _tick++;

            try
            {
                UpdatePreview();
            }
            catch (Exception ex)
            {
                Utils.Log($"BuildPreviewHud.UpdateAfterSimulation: {ex}", 3);
            }
        }

        private void UpdatePreview()
        {
            var builder = MyCubeBuilder.Static;
            var def = builder?.CubeBuilderState?.CurrentBlockDefinition;
            var building = def != null && builder.IsActivated && builder.BlockCreationIsActivated;
            if (!building)
            {
                Hide();
                return;
            }

            var grid = builder.FindClosestGrid();
            if (grid == null)
            {
                Hide();
                return;
            }

            var group = ((IMyCubeGrid)grid).GetGroupComponent();
            if (group == null)
            {
                Hide();
                return;
            }

            // Previewed block bounding box (world). GetBuildBoundingBox is safe while the
            // builder is activated (this is how Build Info positions its held-block overlay).
            var box = builder.GetBuildBoundingBox();
            _boxCenter = box.Center;
            box.GetCorners(_corners, 0);

            var blockId = def.Id.TypeId + "/" + def.Id.SubtypeName;
            var changed = grid.EntityId != _lastGridId || !string.Equals(blockId, _lastBlockId, StringComparison.Ordinal);
            if (changed || _tick >= _nextRecomputeTick)
            {
                _lastGridId = grid.EntityId;
                _lastBlockId = blockId;
                _nextRecomputeTick = _tick + RecomputeIntervalTicks;
                Recompute(group, def);
            }

            Render();
        }

        private void Recompute(GroupComponent group, MyCubeBlockDefinition def)
        {
            var proposed = new ProposedBlock
            {
                Key = new BlockKey(Convert.ToString(def.Id.TypeId).Replace("MyObjectBuilder_", string.Empty),
                    Convert.ToString(def.Id.SubtypeId)),
                Count = 1,
                Pcu = def.PCU
            };

            _results = LimitEvaluation.Evaluate(group, proposed);
        }

        private void Render()
        {
            // Nothing the block counts toward on this grid -> stay hidden (unrestricted block).
            if (_results == null || _results.Count == 0)
            {
                Hide();
                return;
            }

            if (_hudReady && _panel != null)
                RenderPanel();
            else
                RenderFallback();
        }

        private void RenderPanel()
        {
            var camera = MyAPIGateway.Session?.Camera;
            if (camera == null)
            {
                Hide();
                return;
            }

            // Hide when the block is behind the camera (projection would be invalid).
            var view = camera.WorldMatrix;
            if (Vector3D.Dot(_boxCenter - view.Translation, view.Forward) <= 0d)
            {
                _panel.Visible = false;
                return;
            }

            // Anchor at the block's right-most extent along the camera's Right axis, taken at
            // the block's vertical centre, so the text sits just off the right silhouette edge.
            var right = view.Right;
            var extent = 0d;
            for (var i = 0; i < _corners.Length; i++)
            {
                var d = Vector3D.Dot(_corners[i] - _boxCenter, right);
                if (d > extent) extent = d;
            }
            var anchorWorld = _boxCenter + right * extent;

            // Text HUD API has no closing tag; a <color=...> persists until the next one.
            _sb.Clear();
            _sb.Append("<color=").Append(TitleColor).Append(">Ship Core Limits\n");

            for (var i = 0; i < _results.Count; i++)
            {
                var result = _results[i];
                var projected = result.Current + result.Added;
                var color = result.Pass ? PassColor : FailColor;

                _sb.Append("<color=").Append(color).Append('>')
                    .Append(result.Name).Append(": ")
                    .Append(Fmt(projected)).Append('/').Append(Fmt(result.Max));
                if (!result.Pass) _sb.Append("  OVER");
                _sb.Append('\n');
            }

            _panel.Message.Clear().Append(_sb);

            // Project to screen; vertically centre the text about the anchor point.
            var screen = camera.WorldToScreen(ref anchorWorld); // (0,0) centre, x/y in [-1,1]
            var textLength = _panel.GetTextLength();             // X width (>0), Y height (<0)
            _panel.Origin = new Vector2D(screen.X, screen.Y);
            _panel.Offset = new Vector2D(ScreenGapX, -textLength.Y / 2d);
            _panel.Visible = true;
        }

        private void RenderFallback()
        {
            // Degraded mode when Text HUD API isn't installed: a single-line screen notification.
            _sb.Clear();
            var anyFail = false;
            for (var i = 0; i < _results.Count; i++)
            {
                if (_results[i].Pass) continue;
                if (anyFail) _sb.Append(", ");
                else _sb.Append("Ship Core OVER: ");
                _sb.Append(_results[i].Name);
                anyFail = true;
            }

            if (!anyFail) _sb.Append("Ship Core: within limits");

            if (_fallback == null)
                _fallback = MyAPIGateway.Utilities.CreateNotification(string.Empty, 1000, anyFail ? "Red" : "White");

            _fallback.Text = _sb.ToString();
            _fallback.Font = anyFail ? "Red" : "White";
            _fallback.Show(); // re-show refreshes the timer so it stays visible while building
        }

        private void Hide()
        {
            _lastGridId = 0;
            _lastBlockId = string.Empty;

            if (_panel != null) _panel.Visible = false;
            if (_fallback != null) _fallback.Hide();
        }

        private static string Fmt(double value)
        {
            return value.ToString("0.##");
        }
    }
}
