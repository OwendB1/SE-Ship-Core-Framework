using System;
using System.Collections.Generic;
using System.Text;
using Draygo.API;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace ShipCoreFramework
{
    /// <summary>
    /// Client-only build-preview HUD. While the local player holds a block in build mode
    /// and aims at a tracked grid, it evaluates the proposed placement against the grid's
    /// limits (via the shared <see cref="LimitEvaluation"/>) and shows:
    ///  - a capacity panel (screen-space text anchored to the block), and
    ///  - a directional indicator when a facing constraint is violated: a red arrow for the
    ///    block's current facing, a green arrow for the required facing, and a blue 90-degree
    ///    rotation arrow between them (reusing the game's own rotation-gizmo arrow textures).
    ///
    /// Renders through Text HUD API when present, falling back to a HUD notification.
    /// </summary>
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class BuildPreviewHud : MySessionComponentBase
    {
        private const int HudStateOff = 0;         // Session.Config.HudState: 0=Off, 1=Full, 2=Minimal
        private const int HudStateFull = 1;
        private const int RecomputeIntervalTicks = 6; // limit re-evaluation cadence (~10x/sec)
        private const double ScreenGapX = 0.015d; // horizontal gap between block edge and text
        private const double HardCapDisplayFraction = 0.8d; // show a grid-wide hard cap once projected usage hits this fraction
        private const string PassColor = "120,220,120";
        private const string FailColor = "255,90,90";
        private const string TitleColor = "180,200,255";

        // Directional indicator sizing. Icon size is driven by camera distance (not block size)
        // so it keeps a constant apparent size regardless of how large the block is.
        private const double IconScreenFactor = 0.03d;   // world half-size per metre of camera distance
        // Arrows render this fraction of the way from the camera to the block (see materials below).
        private const double ClusterDistanceFraction = 0.35d;
        private const double ClusterDistanceMin = 0.5d;
        private const double ClusterDistanceMax = 6.0d;
        private const double ArrowOffsetMul = 2.2d;      // * icon half-size: arrow centre offset from block centre
        private const double RotationHalfMul = 0.85d;    // * icon half-size
        private const double RotationOffsetMul = 3.0d;   // * icon half-size

        private const BlendTypeEnum Blend = BlendTypeEnum.PostPP;

        // Custom materials in Data/TransparentMaterials.sbc, pointing at the game's rotation-gizmo
        // arrow textures. Their IgnoreDepth is unreliable for mod materials, so the near-camera
        // cluster placement (see Draw) is what actually keeps the arrows drawn over geometry.
        private readonly MyStringId _matArrowGreen = MyStringId.GetOrCompute("SCF_ArrowGreen");
        private readonly MyStringId _matArrowRed = MyStringId.GetOrCompute("SCF_ArrowRed");
        private readonly MyStringId _matArrowBlue = MyStringId.GetOrCompute("SCF_ArrowBlue");
        private readonly MyStringId _matRotationLeft = MyStringId.GetOrCompute("SCF_ArrowLeftBlue");
        private readonly MyStringId _matRotationRight = MyStringId.GetOrCompute("SCF_ArrowRightBlue");
        private readonly MyStringId _matViolationBox = MyStringId.GetOrCompute("SquareFullColor");

        private const double BoxInflate = 1.03d; // slight expansion so the box clears the block surface
        private static readonly Color ViolationColor = new Color(255, 30, 30, 252); // ~99% red

        private struct DirectionIndicator
        {
            internal Vector3D Current; // world direction the block currently faces
            internal Vector3D Correct; // world direction it should face
        }

        private bool _isClient;
        private HudAPIv2 _hudApi;
        private bool _hudReady;
        private HudAPIv2.HUDMessage _panel;
        private IMyHudNotification _fallback;

        private List<LimitCheckResult> _results;
        private Vector3D _boxCenter;
        private MatrixD _boxWorld;
        private Vector3D _boxHalf;
        private readonly Vector3D[] _corners = new Vector3D[8];

        private bool _fullHud;
        private bool _hasViolation;
        private bool _drawIndicators;
        private readonly List<DirectionIndicator> _indicators = new List<DirectionIndicator>();

        // When placing the first core on a coreless grid, show its forward (green) and up (blue) so
        // the player can deliberately set the grid's reference frame for directional limits.
        private bool _drawCoreOrientation;
        private Vector3D _coreForward;
        private Vector3D _coreUp;

        // Panel title reflects the governing config: "<core name> Limits" for an active core, or the
        // SelectedNoCore config's name for a coreless grid.
        private string _panelTitle = "Ship Core Limits";
        // When the group is deactivated or ignored the live engine enforces nothing; the preview
        // then shows a "limits waived" note instead of pass/fail, and draws no box/arrows.
        private bool _limitsWaived;
        private string _waivedReason = string.Empty;

        private int _tick;
        private int _nextRecomputeTick;
        private long _lastGridId;
        private string _lastBlockId = string.Empty;
        private MatrixD _lastOrientation;

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
            // Off HUD -> nothing. The red violation box shows on Full and Minimal; the panel and
            // directional arrows only on Full.
            var config = MyAPIGateway.Session?.Config;
            if (config == null || config.HudState == HudStateOff)
            {
                Hide();
                return;
            }

            _fullHud = config.HudState == HudStateFull;

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

            // Refresh the box transform every frame so the panel/box/arrows track the moving preview.
            var box = builder.GetBuildBoundingBox();
            _boxCenter = box.Center;
            box.GetCorners(_corners, 0);
            _boxHalf = box.HalfExtent;
            var orientation = MatrixD.CreateFromQuaternion(box.Orientation);
            _boxWorld = orientation;
            _boxWorld.Translation = box.Center;

            // But throttle the (allocating) limit evaluation: bucket totals and caps can't change
            // from player input while hovering; block rotation is the one input that affects facing,
            // and the orientation check catches it immediately.
            var blockId = def.Id.TypeId + "/" + def.Id.SubtypeName;
            var changed = grid.EntityId != _lastGridId
                          || !string.Equals(blockId, _lastBlockId, StringComparison.Ordinal)
                          || OrientationChanged(orientation);
            if (changed || _tick >= _nextRecomputeTick)
            {
                _lastGridId = grid.EntityId;
                _lastBlockId = blockId;
                _lastOrientation = orientation;
                _nextRecomputeTick = _tick + RecomputeIntervalTicks;
                Recompute(group, def, orientation, grid);
                UpdateIndicators(group);
            }

            Render();
        }

        private bool OrientationChanged(MatrixD orientation)
        {
            // Block rotations are 90-degree steps, so any real change is large.
            return Vector3D.Dot(orientation.Forward, _lastOrientation.Forward) < 0.9999d
                   || Vector3D.Dot(orientation.Up, _lastOrientation.Up) < 0.9999d;
        }

        private void Recompute(GroupComponent group, MyCubeBlockDefinition def, MatrixD orientation, IMyCubeGrid grid)
        {
            var configName = group.ShipCore?.UniqueName;
            _panelTitle = (string.IsNullOrWhiteSpace(configName) ? "Ship Core" : configName) + " Limits";

            // Qualify the mod's static Session - the MySessionComponentBase.Session property shadows it.
            var modConfig = ShipCoreFramework.Session.Config;
            _drawCoreOrientation = _fullHud
                                   && modConfig != null
                                   && modConfig.IsValidCoreType(def.Id.SubtypeName)
                                   && group.CoreDictionary.Count == 0;
            if (_drawCoreOrientation)
            {
                _coreForward = orientation.Forward;
                _coreUp = orientation.Up;
            }

            // Match what turns OFF on-placement enforcement (per GridComponent.BlockAddedInternal): the
            // group is Deactivated, or the local player is an admin exempt from limits. Deliberately NOT
            // group.IsIgnoredGroup(), which is also true for unowned grids that are still enforced.
            var localExempt = LocalPlayerIsLimitExempt();
            _limitsWaived = group.Deactivated || localExempt;

            var proposed = new ProposedBlock
            {
                Key = new BlockKey(Utils.GetBlockTypeId(def.Id), Utils.GetBlockSubtypeId(def.Id)),
                Count = 1,
                Pcu = def.PCU,
                Mass = ComputeBlockMass(def),
                Orientation = orientation,
                TargetGrid = grid
            };

            _results = LimitEvaluation.Evaluate(group, proposed);

            _hasViolation = false;
            if (_limitsWaived)
            {
                _waivedReason = group.Deactivated ? "core deactivated" : "admin exempt";
                return; // no violation box while waived
            }

            for (var i = 0; i < _results.Count; i++)
            {
                if (_results[i].Pass) continue;
                _hasViolation = true;
                break;
            }
        }

        // True when the local player (the builder) is an admin exempt from limits (ignore-PCU) - the
        // same check GridComponent.BlockAddedInternal uses to bypass enforcement for their placements.
        private static bool LocalPlayerIsLimitExempt()
        {
            var session = MyAPIGateway.Session;
            var player = session?.Player;
            if (player == null) return false;

            var steamId = player.SteamUserId;
            return session.IsUserAdmin(steamId) && session.IsUserIgnorePCULimit(steamId);
        }

        // The block's physical mass, summed from its build components (what the game uses for grid
        // mass). Approximates the added mass for a bare placement; the live cap uses real physics
        // mass (incl. cargo). Zero when unknown, which makes LimitEvaluation skip the MaxMass cap.
        private static float ComputeBlockMass(MyCubeBlockDefinition def)
        {
            var mass = 0f;
            var components = def?.Components;
            if (components == null) return mass;

            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component.Definition != null) mass += component.Definition.Mass * component.Count;
            }

            return mass;
        }

        // For each violated directional limit, record the current and required world facings so
        // Draw() can render the red/green/rotation arrow triad.
        private void UpdateIndicators(GroupComponent group)
        {
            _indicators.Clear();
            _drawIndicators = false;
            if (!_fullHud || _limitsWaived || _results == null) return; // arrows only on the Full HUD, never while waived

            IMyCubeBlock referenceBlock = null;
            for (var i = 0; i < _results.Count; i++)
            {
                var result = _results[i];
                if (result.Kind != LimitCheckKind.Direction || result.Pass || result.SubgridBlocked
                    || result.AllowedDirections == null)
                    continue; // no corrective arrow for subgrid-blocked (rotating wouldn't help)

                if (referenceBlock == null) referenceBlock = group.GetDirectionLockReferenceBlock();
                if (referenceBlock == null) break;

                var current = LimitEvaluation.DirectionToWorld(referenceBlock, result.Facing);
                var correct = ClosestAllowedWorld(referenceBlock, result.AllowedDirections, current);
                _indicators.Add(new DirectionIndicator { Current = current, Correct = correct });
            }

            _drawIndicators = _indicators.Count > 0;
        }

        // The allowed facing requiring the smallest turn from the current facing.
        private static Vector3D ClosestAllowedWorld(IMyCubeBlock referenceBlock, List<DirectionType> allowed,
            Vector3D current)
        {
            var best = LimitEvaluation.DirectionToWorld(referenceBlock, allowed[0]);
            var bestDot = Vector3D.Dot(best, current);
            for (var i = 1; i < allowed.Count; i++)
            {
                var world = LimitEvaluation.DirectionToWorld(referenceBlock, allowed[i]);
                var dot = Vector3D.Dot(world, current);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = world;
                }
            }

            return best;
        }

        private void Render()
        {
            // Panel/fallback only on the Full HUD, and only when there's something worth showing.
            // While waived we still show the panel (title + waived note) so the player understands
            // why nothing is being enforced. (The violation box is handled in Draw so it can also
            // appear on the Minimal HUD.)
            if (!_fullHud || (!_limitsWaived && (_results == null || !HasDisplayableResults())))
            {
                HidePanel();
                return;
            }

            if (_hudReady && _panel != null)
                RenderPanel();
            else
                RenderFallback();
        }

        private bool HasDisplayableResults()
        {
            for (var i = 0; i < _results.Count; i++)
                if (IsDisplayable(_results[i]))
                    return true;
            return false;
        }

        // Grid-wide hard caps stay hidden until the projected usage nears the cap, so common caps
        // (blocks/PCU/mass, which apply to nearly every block) don't clutter the panel constantly.
        // They surface once projected usage reaches HardCapDisplayFraction of the cap, and always
        // when over. Per-type and directional limits are always shown.
        private static bool IsDisplayable(LimitCheckResult result)
        {
            if (result.Kind == LimitCheckKind.MaxBlocks || result.Kind == LimitCheckKind.MaxPcu
                || result.Kind == LimitCheckKind.MaxMass)
            {
                if (!result.Pass) return true;
                return result.Max > 0d && result.Current + result.Added >= result.Max * HardCapDisplayFraction;
            }

            return true;
        }

        private void RenderPanel()
        {
            var camera = MyAPIGateway.Session?.Camera;
            if (camera == null)
            {
                HidePanel();
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

            BuildPanelText();
            _panel.Message.Clear().Append(_sb);

            // Project to screen; vertically centre the text about the anchor point.
            var screen = camera.WorldToScreen(ref anchorWorld); // (0,0) centre, x/y in [-1,1]
            var textLength = _panel.GetTextLength();             // X width (>0), Y height (<0)
            _panel.Origin = new Vector2D(screen.X, screen.Y);
            _panel.Offset = new Vector2D(ScreenGapX, -textLength.Y / 2d);
            _panel.Visible = true;
        }

        private void BuildPanelText()
        {
            // Text HUD API has no closing tag; a <color=...> persists until the next one.
            _sb.Clear();
            _sb.Append("<color=").Append(TitleColor).Append('>').Append(_panelTitle).Append('\n');

            if (_limitsWaived)
            {
                _sb.Append("<color=").Append(PassColor).Append(">Limits waived (").Append(_waivedReason).Append(')');
                return;
            }

            for (var i = 0; i < _results.Count; i++)
            {
                var result = _results[i];
                if (!IsDisplayable(result)) continue;
                _sb.Append("<color=").Append(result.Pass ? PassColor : FailColor).Append('>');
                AppendResultLine(result);
                _sb.Append('\n');
            }
        }

        private void AppendResultLine(LimitCheckResult result)
        {
            if (result.Kind == LimitCheckKind.Direction)
            {
                if (result.SubgridBlocked)
                {
                    _sb.Append(result.Name).Append(": not allowed on subgrid");
                    return;
                }

                _sb.Append(result.Name).Append(": ").Append(result.Facing.ToString());
                if (!result.Pass)
                {
                    _sb.Append(", must be ");
                    for (var i = 0; i < result.AllowedDirections.Count; i++)
                    {
                        if (i > 0) _sb.Append('/');
                        _sb.Append(result.AllowedDirections[i].ToString());
                    }
                }

                return;
            }

            // Pattern: "Limit Name +N (X/Y)"  e.g. "Thrusters +3 (3/4)" / "Thrusters +3 (6/4) OVER"
            var projected = result.Current + result.Added;
            _sb.Append(result.Name).Append(" +").Append(Fmt(result.Added))
                .Append(" (").Append(Fmt(projected)).Append('/').Append(Fmt(result.Max)).Append(')');
            if (!result.Pass) _sb.Append(" OVER");
        }

        public override void Draw()
        {
            if (!_isClient || (!_drawIndicators && !_hasViolation && !_drawCoreOrientation)) return;

            try
            {
                var camera = MyAPIGateway.Session?.Camera;
                if (camera == null) return;

                var view = camera.WorldMatrix;
                var camPos = view.Translation;
                var toBlock = _boxCenter - camPos;
                if (Vector3D.Dot(toBlock, view.Forward) <= 0d) return; // block behind the camera

                if (_hasViolation) DrawViolationBox();

                if (!_drawIndicators && !_drawCoreOrientation) return;

                var distance = toBlock.Length();
                if (distance < 1e-3d) return;

                var clusterDistance = distance * ClusterDistanceFraction;
                if (clusterDistance < ClusterDistanceMin) clusterDistance = ClusterDistanceMin;
                else if (clusterDistance > ClusterDistanceMax) clusterDistance = ClusterDistanceMax;

                var clusterPos = camPos + toBlock / distance * clusterDistance;
                var iconHalfSize = clusterDistance * IconScreenFactor;

                for (var i = 0; i < _indicators.Count; i++)
                    DrawIndicator(_indicators[i], clusterPos, view.Forward, view.Up, iconHalfSize);

                if (_drawCoreOrientation)
                {
                    DrawArrow(_matArrowGreen, _coreForward, clusterPos, view.Forward, view.Up, iconHalfSize); // forward
                    DrawArrow(_matArrowBlue, _coreUp, clusterPos, view.Forward, view.Up, iconHalfSize);       // up
                }
            }
            catch (Exception ex)
            {
                Utils.Log($"BuildPreviewHud.Draw: {ex}", 3);
            }
        }

        private void DrawViolationBox()
        {
            var color = ViolationColor;
            var half = _boxHalf * BoxInflate;
            var localBox = new BoundingBoxD(-half, half);
            var world = _boxWorld;

            MySimpleObjectDraw.DrawTransparentBox(ref world, ref localBox, ref color,
                MySimpleObjectRasterizer.Solid, 1, 0.0f, _matViolationBox, null, false, -1,
                Blend);
        }

        private void DrawIndicator(DirectionIndicator indicator, Vector3D basePos, Vector3D cameraForward,
            Vector3D cameraUp, double iconHalfSize)
        {
            DrawArrow(_matArrowRed, indicator.Current, basePos, cameraForward, cameraUp, iconHalfSize);   // current (wrong) facing
            DrawArrow(_matArrowGreen, indicator.Correct, basePos, cameraForward, cameraUp, iconHalfSize); // required facing
            DrawRotationHint(indicator.Current, indicator.Correct, basePos, cameraForward, cameraUp, iconHalfSize);
        }

        // A straight arrow pointing along 'dir', billboarded toward the camera. The arrow texture
        // points along +up, so up = dir.
        private void DrawArrow(MyStringId material, Vector3D dir, Vector3D basePos, Vector3D cameraForward,
            Vector3D cameraUp, double iconHalfSize)
        {
            var up = dir;
            var left = CameraFacingLeft(up, cameraForward, cameraUp);
            var position = basePos + dir * (iconHalfSize * ArrowOffsetMul);

            MyTransparentGeometry.AddBillboardOriented(material, Vector4.One, position, (Vector3)left, (Vector3)up,
                (float)iconHalfSize, Blend, -1);
        }

        // A 90-degree rotation arrow between the current and required facings, hinting the turn.
        private void DrawRotationHint(Vector3D current, Vector3D correct, Vector3D basePos, Vector3D cameraForward,
            Vector3D cameraUp, double iconHalfSize)
        {
            Vector3D bisector;
            Vector3D axis;
            var offsetMul = RotationOffsetMul;
            if ((current + correct).LengthSquared() < 1e-6d)
            {
                // 180-degree flip: axis is ambiguous, so pick a screen-perpendicular bisector so
                // the rotation arrow arcs over the top between the two opposed straight arrows.
                bisector = Vector3D.Cross(current, cameraForward);
                if (bisector.LengthSquared() < 1e-6d) bisector = Vector3D.Cross(current, cameraUp);
                bisector.Normalize();
                axis = Vector3D.Cross(current, bisector);
                offsetMul *= 0.75d; // pull it ~25% closer to centre in the 180-degree case
            }
            else
            {
                bisector = Vector3D.Normalize(current + correct);
                axis = Vector3D.Cross(current, correct);
            }

            var turnSign = Vector3D.Dot(axis, cameraForward) < 0d ? 1d : -1d;
            var material = turnSign > 0d ? _matRotationRight : _matRotationLeft;

            var up = bisector;
            var left = CameraFacingLeft(up, cameraForward, cameraUp);

            // Rotate the arrow 45 degrees in-plane toward the turn direction so its arc lines up
            // with the current->required sweep.
            const double cos45 = 0.70710678118d;
            var rotatedUp = up * cos45 - left * (turnSign * cos45);
            var rotatedLeft = left * cos45 + up * (turnSign * cos45);
            rotatedUp.Normalize();
            rotatedLeft.Normalize();

            var position = basePos + bisector * (iconHalfSize * offsetMul);

            MyTransparentGeometry.AddBillboardOriented(material, Vector4.One, position, (Vector3)rotatedLeft,
                (Vector3)rotatedUp, (float)(iconHalfSize * RotationHalfMul), Blend, -1);
        }

        private static Vector3D CameraFacingLeft(Vector3D up, Vector3D cameraForward, Vector3D cameraUp)
        {
            var left = Vector3D.Cross(cameraForward, up);
            if (left.LengthSquared() < 1e-6d) left = Vector3D.Cross(cameraUp, up);
            left.Normalize();
            return left;
        }

        private void RenderFallback()
        {
            // Degraded mode when Text HUD API isn't installed: a single-line screen notification.
            _sb.Clear();

            if (_limitsWaived)
            {
                if (_fallback == null)
                    _fallback = MyAPIGateway.Utilities.CreateNotification(string.Empty, 1000, "White");
                _fallback.Text = _panelTitle + ": limits waived (" + _waivedReason + ")";
                _fallback.Font = "White";
                _fallback.Show();
                return;
            }

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
            _drawIndicators = false;
            _hasViolation = false;
            _drawCoreOrientation = false;
            HidePanel();
        }

        private void HidePanel()
        {
            if (_panel != null) _panel.Visible = false;
            if (_fallback != null) _fallback.Hide();
        }

        private static string Fmt(double value)
        {
            return value.ToString("0.##");
        }
    }
}
