using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FoulzExternal.SDK;
using FoulzExternal.storage;
using FoulzExternal.SDK.gamedetector;

namespace FoulzExternal.SDK.caches
{
    internal static class player
    {
        public static List<Instance> CachedPlayers { get; private set; } = new List<Instance>();
        private static Thread? _tid;
        private static bool _vibin = false;
        private static readonly object _lock = new object();

        public static void Start()
        {
            if (_vibin) return;
            _vibin = true;
            _tid = new Thread(loop_cuh) { IsBackground = true };
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
            lock (_lock)
            {
                CachedPlayers = new List<Instance>();
            }
        }

        private static void loop_cuh()
        {
            var list = new List<Instance>();

            while (_vibin)
            {
                var mode = GameType.checking;
                try
                {
                    list.Clear();
                    mode = finder.whatgame();

                    if (mode == GameType.pf)
                    {
                        var ws = Storage.WorkspaceInstance;
                        if (ws.IsValid)
                        {
                            var seen = new HashSet<long>();

                            // Primary: scan Workspace > Ignore for character Models (PF puts characters here)
                            var ignoreFolder = ws.FindFirstChild("Ignore");
                            if (ignoreFolder.IsValid)
                            {
                                var ignoreChildren = ignoreFolder.GetChildren();
                                if (ignoreChildren != null)
                                {
                                    foreach (var model in ignoreChildren)
                                    {
                                        if (model.GetClass() == "Model" && seen.Add(model.Address))
                                            list.Add(model);
                                    }
                                }
                            }

                            // Secondary: also check Workspace > Players > TeamFolders for Models
                            var playersFolder = ws.FindFirstChild("Players");
                            if (playersFolder.IsValid)
                            {
                                foreach (var team in playersFolder.GetChildren())
                                {
                                    if (team.GetClass() != "Folder") continue;
                                    foreach (var model in team.GetChildren())
                                    {
                                        if (model.GetClass() == "Model" && seen.Add(model.Address))
                                            list.Add(model);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        var inst = Storage.PlayersInstance;
                        if (inst.IsValid)
                        {
                            var kids = inst.GetChildren();
                            if (kids != null && kids.Count > 0)
                            {
                                foreach (var k in kids) list.Add(k);
                            }
                        }
                    }

                    if (list.Count >= 1)
                    {
                        lock (_lock)
                        {
                            CachedPlayers = new List<Instance>(list);
                        }
                    }
                }
                catch { }

                Thread.Sleep(mode == GameType.pf ? 1500 : 5000);
            }
        }
    }
}