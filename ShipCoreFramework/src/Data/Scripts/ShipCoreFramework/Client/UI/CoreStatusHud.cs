using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Draygo.API;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Input;
using VRage.ModAPI;
using VRageMath;

namespace ShipCoreFramework
{
    /// <summary>
    /// Client ship-status panel integrated from Core HUD. Reads framework state directly; no mod API loopback.
    /// </summary>
    internal sealed class CoreStatusHud
    {
        private const string SettingsFile = "CoreStatusHud.cfg";
        private const int UpdateIntervalTicks = 10;
        private const int RefreshIntervalUpdates = 30;
        private const int AbilityRefreshIntervalUpdates = 6;
        private const double RaycastDistance = 300d;

        private const string White = "<color=255,255,255>";
        private const string Green = "<color=100,255,100>";
        private const string Yellow = "<color=255,255,100>";
        private const string Red = "<color=255,100,100>";
        private const string Cyan = "<color=100,255,255>";
        private const string Gray = "<color=180,180,180>";

        private readonly StringBuilder _text = new StringBuilder();
        private HudAPIv2.HUDMessage _panel;
        private HudAPIv2.MenuRootCategory _menu;
        private HudAPIv2.MenuItem _toggleMenuItem;
        private HudAPIv2.MenuKeybindInput _keybindMenuItem;
        private IMyHudNotification _fallback;

        private bool _enabled;
        private int _infoLevel = 1;
        private MyKeys _toggleKey = MyKeys.NumPad0;
        private bool _toggleShift;
        private bool _toggleControl;
        private bool _toggleAlt;

        private int _tick;
        private int _refreshCounter;
        private GroupComponent _lastGroup;
        private IMyCubeGrid _lastGrid;
        private string _lastText;

        internal void Load()
        {
            LoadSettings();
            MyAPIGateway.Utilities.MessageEnteredSender += OnMessageEntered;
        }

        internal void OnHudReady()
        {
            _panel = new HudAPIv2.HUDMessage(_text, new Vector2D(-0.98d, 0.55d), null, -1, 1d, true, true)
            {
                Visible = false
            };

            _menu = new HudAPIv2.MenuRootCategory("Ship Core Framework",
                HudAPIv2.MenuRootCategory.MenuFlag.PlayerMenu, "Ship Core Framework");
            _toggleMenuItem = new HudAPIv2.MenuItem(string.Empty, _menu, Toggle);
            _keybindMenuItem = new HudAPIv2.MenuKeybindInput(string.Empty, _menu,
                "Core HUD toggle key", SetToggleKey);
            UpdateMenuText();
        }

        internal void Update()
        {
            if (IsTogglePressed()) Toggle();

            _tick++;
            if (_tick < UpdateIntervalTicks) return;
            _tick = 0;

            if (!_enabled)
            {
                Hide();
                return;
            }

            Vector3D? hitPosition;
            bool controlled;
            IMyCubeGrid grid = GetTargetGrid(out hitPosition, out controlled);
            GroupComponent group = grid?.GetGroupComponent();
            if (grid == null || group == null || !Session.IsServer && !group.HasRuntimeState ||
                !CanView(group, grid))
            {
                Hide();
                return;
            }

            _refreshCounter++;
            int refreshInterval = group.HasRunningAbilityTimer()
                ? AbilityRefreshIntervalUpdates
                : RefreshIntervalUpdates;
            bool refresh = group != _lastGroup || grid != _lastGrid || _lastText == null ||
                           _refreshCounter >= refreshInterval;
            if (refresh)
            {
                _refreshCounter = 0;
                _lastGroup = group;
                _lastGrid = grid;
                _lastText = BuildText(grid, group, hitPosition, controlled);
            }

            Show(_lastText);
        }

        internal void Unload()
        {
            MyAPIGateway.Utilities.MessageEnteredSender -= OnMessageEntered;
            Hide();
            try
            {
                _panel?.DeleteMessage();
            }
            catch (Exception exception)
            {
                Utils.Log("CoreStatusHud message cleanup failed: " + exception.Message, 3);
            }
            _panel = null;
            _menu = null;
            _toggleMenuItem = null;
            _keybindMenuItem = null;
            _fallback = null;
        }

        private void OnMessageEntered(ulong sender, string messageText, ref bool sendToOthers)
        {
            string[] parts = messageText?.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts == null || parts.Length == 0 ||
                !string.Equals(parts[0], "/corehud", StringComparison.OrdinalIgnoreCase)) return;

            sendToOthers = false;
            if (parts.Length == 1)
            {
                Toggle();
                return;
            }

            string value = parts.Length == 2
                ? parts[1]
                : parts.Length == 3 && string.Equals(parts[1], "level", StringComparison.OrdinalIgnoreCase)
                    ? parts[2]
                    : null;
            int level;
            if (!TryParseInfoLevel(value, out level))
            {
                MyAPIGateway.Utilities.ShowNotification(
                    "Usage: /corehud [1|2|3|standard|detailed|full]", 4000);
                return;
            }

            SetInfoLevel(level);
        }

        private bool IsTogglePressed()
        {
            if (_toggleKey == MyKeys.None || MyAPIGateway.Input == null || MyAPIGateway.Gui == null ||
                MyAPIGateway.Gui.ChatEntryVisible || MyAPIGateway.Gui.IsCursorVisible ||
                !MyAPIGateway.Input.IsNewKeyPressed(_toggleKey))
                return false;

            return _toggleShift == MyAPIGateway.Input.IsAnyShiftKeyPressed() &&
                   _toggleControl == MyAPIGateway.Input.IsAnyCtrlKeyPressed() &&
                   _toggleAlt == MyAPIGateway.Input.IsAnyAltKeyPressed();
        }

        private void Toggle()
        {
            _enabled = !_enabled;
            if (!_enabled) Hide();
            SaveSettings();
            UpdateMenuText();
            MyAPIGateway.Utilities.ShowNotification("Core HUD: " + (_enabled ? "Enabled" : "Disabled"), 2000);
        }

        private void SetToggleKey(MyKeys key, bool shift, bool control, bool alt)
        {
            _toggleKey = key;
            _toggleShift = shift;
            _toggleControl = control;
            _toggleAlt = alt;
            SaveSettings();
            UpdateMenuText();
            MyAPIGateway.Utilities.ShowNotification("Core HUD key: " + FormatKeybind(), 2000);
        }

        private void SetInfoLevel(int level)
        {
            _infoLevel = level;
            _lastText = null;
            SaveSettings();
            UpdateMenuText();
            MyAPIGateway.Utilities.ShowNotification("Core HUD info: " + FormatInfoLevel(), 2000);
        }

        private static bool TryParseInfoLevel(string value, out int level)
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out level))
                return level >= 1 && level <= 3;
            if (string.Equals(value, "standard", StringComparison.OrdinalIgnoreCase)) level = 1;
            else if (string.Equals(value, "detailed", StringComparison.OrdinalIgnoreCase)) level = 2;
            else if (string.Equals(value, "full", StringComparison.OrdinalIgnoreCase)) level = 3;
            else return false;
            return true;
        }

        private void UpdateMenuText()
        {
            if (_toggleMenuItem != null)
                _toggleMenuItem.Text = "Core HUD: " + (_enabled ? "Enabled" : "Disabled") +
                                       " (" + FormatInfoLevel() + ")";
            if (_keybindMenuItem != null)
                _keybindMenuItem.Text = "Toggle key: " + FormatKeybind();
        }

        private string FormatKeybind()
        {
            StringBuilder value = new StringBuilder();
            if (_toggleControl) value.Append("Ctrl+");
            if (_toggleShift) value.Append("Shift+");
            if (_toggleAlt) value.Append("Alt+");
            value.Append(_toggleKey == MyKeys.None ? "Disabled" : _toggleKey.ToString());
            return value.ToString();
        }

        private string FormatInfoLevel()
        {
            if (_infoLevel == 1) return "Standard";
            if (_infoLevel == 2) return "Detailed";
            return "Full";
        }

        private string BuildText(IMyCubeGrid grid, GroupComponent group, Vector3D? hitPosition,
            bool controlled)
        {
            _text.Clear();
            ShipCore core = group.ShipCore;

            _text.Append(Cyan).AppendLine(grid.CustomName).Append(White);
            if (core != null && !string.IsNullOrWhiteSpace(core.UniqueName))
            {
                _text.Append(Yellow).Append(group.CoreCount > 0 ? "Core: " : "Class: ")
                    .Append(core.UniqueName);
                if (group.CoreCount == 0) _text.Append(" (No Core)");
                _text.AppendLine().Append(White);
            }
            if (group.Deactivated)
                _text.Append(Red).AppendLine("[DEACTIVATED - limits inactive]").Append(White);

            if (_infoLevel >= 3 && hitPosition.HasValue && MyAPIGateway.Session?.Camera != null)
            {
                double distance = Vector3D.Distance(MyAPIGateway.Session.Camera.WorldMatrix.Translation,
                    hitPosition.Value);
                _text.Append(Gray).Append(distance.ToString("F0", CultureInfo.InvariantCulture))
                    .AppendLine("m away").Append(White);
            }
            else if (_infoLevel >= 3 && controlled)
            {
                _text.Append(Gray).AppendLine("(Piloting)").Append(White);
            }

            if (_infoLevel >= 2)
            {
                _text.AppendLine();
                AppendUsage("Blocks", group.GroupBlocksCount, group.GetEffectiveMaxBlocks(), string.Empty);
                AppendUsage("Mass", group.GroupMass, group.GetEffectiveMaxMass(), " kg");
                AppendUsage("PCU", group.GroupPCU, group.GetEffectiveMaxPCU(), string.Empty);
            }

            if (_infoLevel >= 3) AppendLimits(group, core);
            _text.AppendLine().Append(Gray).AppendLine("--- Status ---").Append(White);
            if (_infoLevel >= 2) AppendSpeed(grid, group, _infoLevel >= 3);
            AppendAbilities(group, core);
            return _text.ToString();
        }

        private void AppendUsage(string label, double current, double max, string suffix)
        {
            _text.Append(label).Append(": ").Append(StatusColor(current, max))
                .Append(current.ToString("N0", CultureInfo.InvariantCulture)).Append(White);
            if (max > 0d)
            {
                _text.Append(" / ").Append(max.ToString("N0", CultureInfo.InvariantCulture)).Append(suffix);
                if (current > max) _text.Append(" !");
            }
            else
            {
                _text.Append(suffix);
            }
            _text.AppendLine();
        }

        private void AppendLimits(GroupComponent group, ShipCore core)
        {
            BlockLimit[] configured = core?.BlockLimits;
            if (configured == null || configured.Length == 0) return;

            _text.AppendLine().Append(Gray).AppendLine("--- Block Limits ---").Append(White);
            for (int index = 0; index < configured.Length; index++)
            {
                BlockLimit limit = configured[index];
                if (limit == null) continue;
                LimitBucket bucket;
                double current = 0d;
                if (group.Limits.TryGetValue(limit, out bucket) && bucket != null)
                {
                    lock (bucket.BucketLock) current = bucket.TotalWeight;
                }
                double max = group.GetEffectiveMaxCount(limit);
                _text.Append(limit.Name).Append(": ").Append(StatusColor(current, max))
                    .Append(current.ToString("F0", CultureInfo.InvariantCulture)).Append(White)
                    .Append(" / ").Append(max.ToString("F0", CultureInfo.InvariantCulture));
                if (current > max) _text.Append(" !");
                _text.AppendLine();
            }
        }

        private void AppendSpeed(IMyCubeGrid grid, GroupComponent group, bool includeForwardSpeed)
        {
            float effectiveSpeed;
            bool boostActive;
            lock (group.SpeedStateLock)
            {
                effectiveSpeed = group.EffectiveSpeedLimitMetersPerSecond;
                boostActive = group.EffectiveBoostEnabled;
            }

            _text.Append("Max Speed: ").Append(boostActive ? Cyan : White)
                .Append(effectiveSpeed.ToString("F0", CultureInfo.InvariantCulture)).Append(" m/s");
            if (boostActive) _text.Append(" [BOOST]");
            _text.AppendLine().Append(White);

            if (!includeForwardSpeed) return;
            float? forwardSpeed = CalculateForwardFrictionSpeed(grid, group);
            if (forwardSpeed.HasValue)
                _text.Append("Fwd Speed: ").Append(Green)
                    .Append(forwardSpeed.Value.ToString("F0", CultureInfo.InvariantCulture))
                    .AppendLine(" m/s").Append(White);
        }

        private void AppendAbilities(GroupComponent group, ShipCore core)
        {
            if (core == null) return;

            bool boostActive;
            float boostDuration;
            float boostCooldown;
            bool defenseActive;
            float defenseDuration;
            float defenseCooldown;
            bool powerActive;
            float powerDuration;
            float powerCooldown;
            group.GetAbilityTimers(out boostActive, out boostDuration, out boostCooldown,
                out defenseActive, out defenseDuration, out defenseCooldown,
                out powerActive, out powerDuration, out powerCooldown);

            if (core.SpeedBoostEnabled)
                AppendAbility("Boost", boostActive, boostDuration, boostCooldown);
            if (core.EnableActiveDefenseModifiers)
                AppendAbility("Active Defense", defenseActive, defenseDuration, defenseCooldown);
            if (core.PowerOverclockEnabled)
                AppendAbility("Power Increase", powerActive, powerDuration, powerCooldown);
        }

        private void AppendAbility(string label, bool active, float duration, float cooldown)
        {
            _text.Append(label).Append(": ");
            if (active)
                _text.Append(Cyan).Append("ACTIVE ").Append(FormatTimer(duration));
            else if (cooldown > 0f)
                _text.Append(Yellow).Append("COOLDOWN ").Append(FormatTimer(cooldown));
            else
                _text.Append(Green).Append("READY");
            _text.AppendLine().Append(White);
        }

        private static string FormatTimer(float ticks)
        {
            int seconds = (int)Math.Ceiling(Math.Max(0f, ticks) / 60f);
            if (seconds < 60) return seconds.ToString(CultureInfo.InvariantCulture) + "s";
            return (seconds / 60).ToString(CultureInfo.InvariantCulture) + ":" +
                   (seconds % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        private static float? CalculateForwardFrictionSpeed(IMyCubeGrid grid, GroupComponent group)
        {
            SpeedModifiers speed = group.SpeedModifiers;
            if (speed == null || !group.GetFrictionEnforcementEnabled() ||
                speed.FrictionCurve != null && speed.FrictionCurve.HasSegments())
                return null;

            float maxDeceleration = group.GetFrictionMaximumDecelerationOverride();
            if (maxDeceleration < 0f) maxDeceleration = speed.MaximumFrictionDeceleration;
            if (maxDeceleration <= 0f || grid?.Physics == null || grid.Physics.Mass <= 0f) return null;

            float minimum;
            float maximum;
            if (Session.Config.FrictionSpeedValueMode == FrictionSpeedValueMode.Absolute)
            {
                minimum = group.GetMinimumFrictionSpeedAbsoluteOverride();
                if (minimum < 0f) minimum = speed.MinimumFrictionSpeedAbsolute;
                maximum = group.GetMaximumFrictionSpeedAbsoluteOverride();
                if (maximum < 0f) maximum = speed.MaximumFrictionSpeedAbsolute;
            }
            else
            {
                minimum = group.GetMinimumFrictionSpeedModifierOverride();
                if (minimum < 0f) minimum = speed.MinimumFrictionSpeedModifier;
                maximum = group.GetMaximumFrictionSpeedModifierOverride();
                if (maximum < 0f) maximum = speed.MaximumFrictionSpeedModifier;
                minimum *= Session.Config.MaxPossibleSpeedMetersPerSecond;
                maximum *= Session.Config.MaxPossibleSpeedMetersPerSecond;
            }

            float effectiveSpeed;
            bool boostActive;
            lock (group.SpeedStateLock)
            {
                effectiveSpeed = group.EffectiveSpeedLimitMetersPerSecond;
                boostActive = group.EffectiveBoostEnabled;
            }
            if (!boostActive && maximum > 0f) effectiveSpeed = Math.Min(effectiveSpeed, maximum);
            maximum = effectiveSpeed;
            if (maximum <= 0f) return null;

            float thrust = GetForwardThrust(grid);
            if (thrust <= 0f) return null;
            float ratio = thrust / (grid.Physics.Mass * maxDeceleration);
            ratio = MathHelper.Clamp(ratio, 0f, 1f);
            return Math.Min(effectiveSpeed, Math.Max(0f, minimum) + ratio * (maximum - Math.Max(0f, minimum)));
        }

        private static float GetForwardThrust(IMyCubeGrid grid)
        {
            IMyGridTerminalSystem terminal = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid);
            if (terminal == null) return 0f;
            List<IMyThrust> thrusters = new List<IMyThrust>();
            terminal.GetBlocksOfType(thrusters);
            Vector3I backward = Base6Directions.GetIntVector(Base6Directions.Direction.Backward);
            float total = 0f;
            for (int index = 0; index < thrusters.Count; index++)
            {
                IMyThrust thruster = thrusters[index];
                if (thruster.IsWorking && thruster.CubeGrid == grid && thruster.GridThrustDirection == backward)
                    total += thruster.MaxEffectiveThrust;
            }
            return total;
        }

        private static string StatusColor(double current, double max)
        {
            if (max <= 0d) return White;
            double ratio = current / max;
            if (ratio >= 1d) return Red;
            return ratio >= 0.9d ? Yellow : Green;
        }

        private void Show(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Hide();
                return;
            }

            if (_panel != null)
            {
                _text.Clear().Append(value);
                _panel.Visible = true;
                return;
            }

            string preview = BuildFallbackPreview(value);
            if (_fallback == null) _fallback = MyAPIGateway.Utilities.CreateNotification(preview, 250, "White");
            _fallback.Text = preview;
            _fallback.ResetAliveTime();
            _fallback.Show();
        }

        private void Hide()
        {
            _text.Clear();
            if (_panel != null) _panel.Visible = false;
            _fallback?.Hide();
            _lastGroup = null;
            _lastGrid = null;
            _lastText = null;
            _refreshCounter = 0;
        }

        private static string BuildFallbackPreview(string value)
        {
            string[] lines = value.Split('\n');
            StringBuilder preview = new StringBuilder();
            for (int index = 0; index < lines.Length && index < 3; index++)
            {
                if (index > 0) preview.Append(" | ");
                string line = lines[index];
                int tag;
                while ((tag = line.IndexOf("<color=", StringComparison.Ordinal)) >= 0)
                {
                    int end = line.IndexOf('>', tag);
                    if (end < 0) break;
                    line = line.Remove(tag, end - tag + 1);
                }
                preview.Append(line);
            }
            return preview.ToString();
        }

        private static IMyCubeGrid GetTargetGrid(out Vector3D? hitPosition, out bool controlled)
        {
            hitPosition = null;
            controlled = false;
            IMyShipController controller = MyAPIGateway.Session?.Player?.Controller?.ControlledEntity as IMyShipController;
            if (controller?.CubeGrid != null)
            {
                controlled = true;
                return controller.CubeGrid;
            }

            IMyCamera camera = MyAPIGateway.Session?.Camera;
            if (camera == null) return null;
            Vector3D start = camera.WorldMatrix.Translation;
            Vector3D end = start + camera.WorldMatrix.Forward * RaycastDistance;
            IHitInfo hit;
            if (!MyAPIGateway.Physics.CastRay(start, end, out hit) || hit?.HitEntity == null) return null;
            hitPosition = hit.Position;

            IMyCubeGrid grid = hit.HitEntity as IMyCubeGrid;
            if (grid != null) return grid;
            IMyCubeBlock block = hit.HitEntity as IMyCubeBlock;
            if (block?.CubeGrid != null) return block.CubeGrid;
            return hit.HitEntity.Parent as IMyCubeGrid;
        }

        private static bool CanView(GroupComponent group, IMyCubeGrid grid)
        {
            IMyPlayer player = MyAPIGateway.Session?.Player;
            if (player == null) return false;
            if (player.PromoteLevel == MyPromoteLevel.Admin || player.PromoteLevel == MyPromoteLevel.Owner ||
                MyAPIGateway.Session.CreativeMode)
                return true;

            long ownerId = group.OwnerId;
            if (ownerId == 0 && grid.BigOwners != null && grid.BigOwners.Count > 0)
                ownerId = grid.BigOwners[0];
            if (ownerId == player.IdentityId) return true;

            IMyFaction playerFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(player.IdentityId);
            IMyFaction ownerFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(ownerId);
            return playerFaction != null && ownerFaction != null &&
                   playerFaction.FactionId == ownerFaction.FactionId;
        }

        private void LoadSettings()
        {
            try
            {
                if (!MyAPIGateway.Utilities.FileExistsInLocalStorage(SettingsFile, typeof(CoreStatusHud))) return;
                using (TextReader reader = MyAPIGateway.Utilities.ReadFileInLocalStorage(SettingsFile,
                           typeof(CoreStatusHud)))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        string[] pair = line.Split(new[] { '=' }, 2);
                        if (pair.Length != 2) continue;
                        bool flag;
                        int key;
                        int level;
                        if (pair[0] == "Enabled" && bool.TryParse(pair[1], out flag)) _enabled = flag;
                        else if (pair[0] == "Level" && int.TryParse(pair[1], NumberStyles.Integer,
                                     CultureInfo.InvariantCulture, out level) && level >= 1 && level <= 3)
                            _infoLevel = level;
                        else if (pair[0] == "Key" && int.TryParse(pair[1], NumberStyles.Integer,
                                     CultureInfo.InvariantCulture, out key) &&
                                 Enum.IsDefined(typeof(MyKeys), (MyKeys)key))
                            _toggleKey = (MyKeys)key;
                        else if (pair[0] == "Shift" && bool.TryParse(pair[1], out flag)) _toggleShift = flag;
                        else if (pair[0] == "Control" && bool.TryParse(pair[1], out flag)) _toggleControl = flag;
                        else if (pair[0] == "Alt" && bool.TryParse(pair[1], out flag)) _toggleAlt = flag;
                    }
                }
            }
            catch (Exception exception)
            {
                Utils.Log("CoreStatusHud settings load failed: " + exception.Message, 2);
            }
        }

        private void SaveSettings()
        {
            try
            {
                using (TextWriter writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(SettingsFile,
                           typeof(CoreStatusHud)))
                {
                    writer.WriteLine("Enabled=" + _enabled);
                    writer.WriteLine("Level=" + _infoLevel.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("Key=" + ((int)_toggleKey).ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("Shift=" + _toggleShift);
                    writer.WriteLine("Control=" + _toggleControl);
                    writer.WriteLine("Alt=" + _toggleAlt);
                }
            }
            catch (Exception exception)
            {
                Utils.Log("CoreStatusHud settings save failed: " + exception.Message, 2);
            }
        }
    }
}
