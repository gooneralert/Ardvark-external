using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FoulzExternal.SDK;
using FoulzExternal.storage;
using FoulzExternal.SDK.gamedetector;
using Offsets;

namespace FoulzExternal.SDK.caches
{
    public struct RobloxPlayer
    {
        public long address;
        public string Name;
        public Instance Team;
        public Instance Character;
        public Instance Humanoid;
        public float Health;
        public float MaxHealth;
        public int RigType;
        public Instance Head;
        public Instance HumanoidRootPart;
        public Instance Left_Arm;
        public Instance Left_Leg;
        public Instance Right_Arm;
        public Instance Right_Leg;
        public Instance Torso;
        public Instance Upper_Torso;
        public Instance Lower_Torso;
        public Instance Right_Upper_Arm;
        public Instance Right_Lower_Arm;
        public Instance Right_Hand;
        public Instance Left_Upper_Arm;
        public Instance Left_Lower_Arm;
        public Instance Left_Hand;
        public Instance Right_Upper_Leg;
        public Instance Right_Lower_Leg;
        public Instance Right_Foot;
        public Instance Left_Upper_Leg;
        public Instance Left_Lower_Leg;
        public Instance Left_Foot;
        public Instance TeammateLabel;
        // PF-specific fields
        public List<Instance> Bones;
        public Instance Tool;
        public string ToolName;
        public bool IsPF;
        public uint DotColor; // BackgroundColor3 of BillboardGui > playertag > dot
    }

    internal static class playerobjects
    {
        public static List<RobloxPlayer> CachedPlayerObjects { get; private set; } = new List<RobloxPlayer>();
        private static Thread? _tid;
        private static bool _vibin = false;
        private static readonly object _sync = new object();

        public static void Start()
        {
            if (_vibin) return;
            _vibin = true;
            _tid = new Thread(loop_bro) { IsBackground = true };
            _tid.Start();
        }

        public static void Stop()
        {
            _vibin = false;
            try { _tid?.Join(200); } catch { }
            _tid = null;
            Clear();
        }

        public static void Clear()
        {
            lock (_sync)
            {
                CachedPlayerObjects.Clear();
            }
        }

        // PF weapon name cache (persists across iterations like C++ static map)
        private static readonly Dictionary<long, string> _pfWeaponCache = new Dictionary<long, string>();

        // Tracks last game type for one-shot auto-enable of PF settings
        private static GameType _lastAutoGame = GameType.unknownshi;

        // Cached local player's dot color — updated once per cache cycle
        public static long CachedLocalPFTeamAddress { get; private set; } = 0;
        public static uint CachedLocalPFDotColor { get; private set; } = 0;
        public static string CachedLocalPFTeamName { get; private set; } = "";

        // Determines which PF team ("Phantoms" or "Ghosts") a character model belongs to
        // by checking if any child inside a Folder in the model has Color3 == 0x5a4b36 (Phantoms brown).
        public static Instance GetPFTeam(Instance playerModel)
        {
            long parentAddr = 0;
            try { parentAddr = Instance.Mem.ReadPtr(playerModel.Address + Offsets.Instance.Parent); } catch { }

            if (playerModel.Address != 0 && parentAddr != 0)
            {
                var folder = playerModel.FindFirstChildOfClass("Folder");
                if (folder.IsValid)
                {
                    var children = folder.GetChildren();
                    if (children != null)
                    {
                        foreach (var child in children)
                        {
                            try
                            {
                                uint color = Instance.Mem.Read<uint>(child.Address + Offsets.BasePart.Color3);
                                if (color == 0x5a4b36u)
                                {
                                    var teams = Storage.DataModelInstance.FindFirstChildOfClass("Teams");
                                    if (teams.IsValid)
                                        return teams.FindFirstChild("Phantoms");
                                }
                            }
                            catch { }
                        }
                    }
                }
            }

            var teamsDefault = Storage.DataModelInstance.FindFirstChildOfClass("Teams");
            if (teamsDefault.IsValid)
                return teamsDefault.FindFirstChild("Ghosts");

            return new Instance(0);
        }

        // Returns all PF player character models from Workspace > Ignore (no team filtering here).
        public static List<Instance> GetPFPlayerModels(bool teamCheckEnabled)
        {
            var targetPlayers = new List<Instance>();
            var seen = new HashSet<long>();

            var ws = Storage.WorkspaceInstance;
            if (!ws.IsValid) return targetPlayers;

            // Primary: Workspace > Ignore — contains all PF character models
            var ignoreFolder = ws.FindFirstChild("Ignore");
            if (ignoreFolder.IsValid)
            {
                var ignoreChildren = ignoreFolder.GetChildren();
                if (ignoreChildren != null)
                {
                    foreach (var child in ignoreChildren)
                    {
                        try
                        {
                            if (child.GetClass() == "Model" && seen.Add(child.Address))
                                targetPlayers.Add(child);
                        }
                        catch { }
                    }
                }
            }

            // Fallback: Workspace > Players > [team folders]
            var playersFolder = ws.FindFirstChild("Players");
            if (playersFolder.IsValid)
            {
                var teamFolders = playersFolder.GetChildren();
                if (teamFolders != null)
                {
                    foreach (var team in teamFolders)
                    {
                        var teamPlayers = team.GetChildren();
                        if (teamPlayers == null) continue;
                        foreach (var player in teamPlayers)
                        {
                            try
                            {
                                if (player.GetClass() == "Model" && seen.Add(player.Address))
                                    targetPlayers.Add(player);
                            }
                            catch { }
                        }
                    }
                }
            }

            return targetPlayers;
        }

        private static string ReadTextLabelText(Instance textLabel)
        {
            if (!textLabel.IsValid) return "";
            try
            {
                long textAddr = textLabel.Address + Offsets.GuiObject.Text;
                int len = Instance.Mem.Read<int>(textAddr + 0x18);
                if (len <= 0 || len > 256) return "";
                if (len >= 16)
                {
                    long ptr = Instance.Mem.ReadPtr(textAddr);
                    return ptr != 0 ? Instance.Mem.ReadString(ptr) : "";
                }
                return Instance.Mem.ReadString(textAddr);
            }
            catch { return ""; }
        }

        // Reads the dot color from a model's bones: bone > BillboardGui > playertag > dot > BackgroundColor3
        private static uint ReadDotColorFromModel(Instance model)
        {
            try
            {
                var children = model.GetChildren();
                if (children == null) return 0;
                foreach (var bone in children)
                {
                    try
                    {
                        var boneKids = bone.GetChildren();
                        if (boneKids == null) continue;
                        foreach (var bc in boneKids)
                        {
                            try
                            {
                                if (bc.GetClass() != "BillboardGui") continue;
                                var pt = bc.FindFirstChild("playertag");
                                if (!pt.IsValid) continue;
                                var dot = pt.FindFirstChild("dot");
                                if (!dot.IsValid) continue;
                                uint c = Instance.Mem.Read<uint>(dot.Address + GuiObject.BackgroundColor3);
                                if (c != 0) return c;
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return 0;
        }

        private static void CachePFPlayers(List<RobloxPlayer> list, GameType currentGame)
        {
            // Structure (from dx9 lua):
            //   Workspace > Players > [teamFolder0, teamFolder1]
            //   each teamFolder > [playerModel, ...]
            //   each playerModel > Part children (flat, no nested folders)
            //   Part with Decal child  = head
            //   Part with SpotLight child = lower body

            var lp = Storage.LocalPlayerInstance;
            if (!lp.IsValid) return;

            string localName = "";
            try { localName = lp.GetName(); } catch { }

            bool teamCheck = Options.Settings.Checks.PFTeamCheck;

            var ws = Storage.WorkspaceInstance;
            if (!ws.IsValid) return;

            var playersF = ws.FindFirstChild("Players");
            if (!playersF.IsValid) return;

            List<Instance>? teamFolders = null;
            try { teamFolders = playersF.GetChildren(); } catch { }
            if (teamFolders == null || teamFolders.Count < 2) return;

            // Auto-detect which folder contains the local player's model by scanning names.
            // This avoids relying on a hardcoded index that breaks when PF shuffles folder order.
            // Falls back to PFSwitchTeam index when names are encrypted/unavailable.
            int ownTeamIdx = -1;
            if (!string.IsNullOrEmpty(localName))
            {
                for (int fi = 0; fi < teamFolders.Count && ownTeamIdx < 0; fi++)
                {
                    List<Instance>? fmembers = null;
                    try { fmembers = teamFolders[fi].GetChildren(); } catch { }
                    if (fmembers == null) continue;
                    foreach (var mm in fmembers)
                    {
                        try { if (mm.GetName() == localName) { ownTeamIdx = fi; break; } }
                        catch { }
                    }
                }
            }
            if (ownTeamIdx < 0)
                ownTeamIdx = Options.Settings.Checks.PFSwitchTeam ? 0 : 1;

            // Update debug statics
            CachedLocalPFDotColor = 0;
            CachedLocalPFTeamAddress = 1; // always non-zero = detected
            try { CachedLocalPFTeamName = teamFolders[ownTeamIdx].GetName(); }
            catch { CachedLocalPFTeamName = $"folder[{ownTeamIdx}]"; }

            for (int ti = 0; ti < teamFolders.Count; ti++)
            {
                // Skip own team folder when PF team check is on
                if (teamCheck && ti == ownTeamIdx) continue;

                List<Instance>? teamMembers = null;
                try { teamMembers = teamFolders[ti].GetChildren(); } catch { }
                if (teamMembers == null) continue;

                foreach (var playerModel in teamMembers)
                {
                    try
                    {
                        if (!playerModel.IsValid) continue;

                        // Skip own character by name (best-effort; PF may encrypt names so this may not match)
                        if (!string.IsNullOrEmpty(localName))
                        {
                            string mName = "";
                            try { mName = playerModel.GetName(); } catch { }
                            if (mName == localName) continue;
                        }

                        var info = new RobloxPlayer { address = playerModel.Address };
                        info.Bones = new List<Instance>();
                        info.IsPF = true;
                        info.Health = 100f; // PF health not readable from character model; assume alive

                        // Iterate direct Part children — matches dx9 lua structure exactly
                        List<Instance>? bodyParts = null;
                        try { bodyParts = playerModel.GetChildren(); } catch { }
                        if (bodyParts == null) continue;

                        foreach (var part in bodyParts)
                        {
                            try
                            {
                                if (!part.IsValid) continue;
                                string partClass = part.GetClass();
                                if (partClass != "Part" && partClass != "MeshPart") continue;

                                info.Bones.Add(part);

                                // Head: Part with a Decal child (helmet area — highestPoint in lua)
                                if (!info.Head.IsValid)
                                {
                                    var decal = part.FindFirstChildOfClass("Decal");
                                    if (decal.IsValid) info.Head = part;
                                }

                                // Lower body: Part with a SpotLight child (lowestPoint in lua)
                                if (!info.HumanoidRootPart.IsValid)
                                {
                                    var spotlight = part.FindFirstChildOfClass("SpotLight");
                                    if (spotlight.IsValid) info.HumanoidRootPart = part;
                                }
                            }
                            catch { }
                        }

                        if (info.Bones.Count == 0) continue;

                        // Fallbacks if Decal/SpotLight parts not found
                        if (!info.Head.IsValid) info.Head = info.Bones[0];
                        if (!info.HumanoidRootPart.IsValid) info.HumanoidRootPart = info.Bones[info.Bones.Count / 2];

                        list.Add(info);
                    }
                    catch { continue; }
                }
            }
        }  // end CachePFPlayers

        private static double TryParseHealthValue(Instance frame)
        {
            if (!frame.IsValid) return -1.0;
            try
            {
                var textLabel = frame.FindFirstChildOfClass("TextLabel");
                if (textLabel.IsValid)
                {
                    string txt = ReadTextLabelText(textLabel);
                    if (!string.IsNullOrEmpty(txt))
                    {
                        string num = "";
                        foreach (char c in txt)
                        {
                            if ((c >= '0' && c <= '9') || c == '.') num += c;
                        }
                        if (!string.IsNullOrEmpty(num) && double.TryParse(num, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v))
                            return v;
                    }
                }
            }
            catch { }
            return -1.0;
        }

        private static void loop_bro()
        {
            var list_cuh = new List<RobloxPlayer>();

            while (_vibin)
            {
                try
                {
                    list_cuh.Clear();
                    var currentGame = finder.whatgame();

                    // Auto-enable PFTeamCheck once each time PF is entered
                    if (currentGame == GameType.pf && _lastAutoGame != GameType.pf)
                    {
                        Options.Settings.Checks.PFTeamCheck = true;
                        _lastAutoGame = GameType.pf;
                        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                        {
                            var win = System.Windows.Application.Current?.MainWindow as FoulzExternal.MainWindow;
                            if (win?.PFTeamCheckToggle != null)
                            {
                                win.PFTeamCheckToggle.IsChecked = true;
                                win.PFTeamCheckToggle.Content = "ON";
                            }
                        });
                    }
                    else if (currentGame != GameType.pf && currentGame != GameType.checking)
                    {
                        _lastAutoGame = currentGame;
                    }

                    if (currentGame == GameType.pf)
                    {
                        CachePFPlayers(list_cuh, currentGame);
                    }
                    else
                    {
                        var plrs = player.CachedPlayers.ToList();

                        foreach (var inst in plrs)
                        {
                            if (!inst.IsValid) continue;

                            var info = new RobloxPlayer();
                            info.address = inst.Address;
                            info.Name = inst.GetName();

                            try { info.Team = new Instance(Instance.Mem.ReadPtr(inst.Address + Offsets.Player.Team)); } catch { }
                            info.Character = inst.GetCharacter();

                            if (!info.Character.IsValid) continue;

                            info.Humanoid = info.Character.FindFirstChildOfClass("Humanoid");
                            if (!info.Humanoid.IsValid) continue;

                            try
                            {
                                info.Health = Instance.Mem.Read<float>(info.Humanoid.Address + Offsets.Humanoid.Health);
                                info.MaxHealth = Instance.Mem.Read<float>(info.Humanoid.Address + Offsets.Humanoid.MaxHealth);
                                info.RigType = Instance.Mem.Read<int>(info.Humanoid.Address + Offsets.Humanoid.RigType);
                            }
                            catch { info.Health = 100f; info.MaxHealth = 100f; info.RigType = 0; }

                            var c = info.Character;
                            info.Head = c.FindFirstChild("Head");
                            info.HumanoidRootPart = c.FindFirstChild("HumanoidRootPart");

                            if (currentGame == GameType.rivals && info.HumanoidRootPart.IsValid)
                            {
                                info.TeammateLabel = info.HumanoidRootPart.FindFirstChild("TeammateLabel");
                            }

                            if (!info.Head.IsValid) continue;

                            if (info.RigType == 0)
                            {
                                info.Left_Arm = c.FindFirstChild("Left Arm");
                                info.Left_Leg = c.FindFirstChild("Left Leg");
                                info.Right_Arm = c.FindFirstChild("Right Arm");
                                info.Right_Leg = c.FindFirstChild("Right Leg");
                                info.Torso = c.FindFirstChild("Torso");
                            }
                            else
                            {
                                info.Upper_Torso = c.FindFirstChild("UpperTorso");
                                info.Lower_Torso = c.FindFirstChild("LowerTorso");
                                info.Right_Upper_Arm = c.FindFirstChild("RightUpperArm");
                                info.Right_Lower_Arm = c.FindFirstChild("RightLowerArm");
                                info.Right_Hand = c.FindFirstChild("RightHand");
                                info.Left_Upper_Arm = c.FindFirstChild("LeftUpperArm");
                                info.Left_Lower_Arm = c.FindFirstChild("LeftLowerArm");
                                info.Left_Hand = c.FindFirstChild("LeftHand");
                                info.Right_Upper_Leg = c.FindFirstChild("RightUpperLeg");
                                info.Right_Lower_Leg = c.FindFirstChild("RightLowerLeg");
                                info.Right_Foot = c.FindFirstChild("RightFoot");
                                info.Left_Upper_Leg = c.FindFirstChild("LeftUpperLeg");
                                info.Left_Lower_Leg = c.FindFirstChild("LeftLowerLeg");
                                info.Left_Foot = c.FindFirstChild("LeftFoot");
                            }

                            list_cuh.Add(info);
                        }
                    }

                    lock (_sync)
                    {
                        CachedPlayerObjects = new List<RobloxPlayer>(list_cuh);
                    }
                }
                catch { }
                Thread.Sleep(500);
            }
        }
    }
}