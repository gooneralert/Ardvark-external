using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FoulzExternal.SDK;
using FoulzExternal.storage;

namespace FoulzExternal.features.games.universal.explorer
{
    public partial class ExplorerControl : UserControl
    {
        private readonly DispatcherTimer t;
        private readonly Dictionary<string, BitmapImage> cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<long> expanded = new();
        private readonly HashSet<long> flagged = new();
        private long selected_addr;
        private string selected_class = "";
        private bool tree_loaded;
        private long last_datamodel;
        private long last_placeid;

        private static readonly HashSet<string> TeleportableClasses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Part", "MeshPart", "SpawnLocation", "UnionOperation"
        };

        public ExplorerControl()
        {
            InitializeComponent();
            t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2000) };
            t.Tick += (s, e) => refresh();
            t.Start();
            tree.SelectedItemChanged += (s, e) => set_props();
        }

        private void set_props()
        {
            if (tree.SelectedItem is not TreeViewItem tvi || tvi.Tag is not long addr)
            {
                clear_props();
                return;
            }

            selected_addr = addr;
            var i = new Instance(addr);
            string name = i.GetName() ?? "Unnamed";
            string cls = i.GetClass() ?? "???";
            selected_class = cls;

            p_name.Text = name;
            p_class.Text = cls;
            p_addr.Text = $"0x{addr:X}";
            p_flagged.Text = flagged.Contains(addr) ? "Yes" : "No";
            p_flagged.Foreground = flagged.Contains(addr)
                ? new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B))
                : new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77));

            // children count
            try
            {
                var kids = i.GetChildren();
                p_children.Text = kids.Count.ToString();
            }
            catch { p_children.Text = "-"; }

            // health
            var hum = i.GetHumanoid();
            p_hp.Text = hum.IsValid ? $"{hum.GetHealth():0}/{hum.GetMaxHealth():0}" : "-";

            // position (for BaseParts)
            if (TeleportableClasses.Contains(cls))
            {
                try
                {
                    var pos = i.GetPosition();
                    p_pos.Text = $"{pos.x:F1}, {pos.y:F1}, {pos.z:F1}";
                }
                catch { p_pos.Text = "-"; }
                btn_teleport.Visibility = Visibility.Visible;
            }
            else
            {
                p_pos.Text = "-";
                btn_teleport.Visibility = Visibility.Collapsed;
            }

            // ProximityPrompt
            if (cls == "ProximityPrompt")
            {
                pnl_proximity.Visibility = Visibility.Visible;
                try
                {
                    float hold = Instance.Mem.Read<float>(addr + Offsets.ProximityPrompt.HoldDuration);
                    sld_hold.Value = hold;
                    txt_hold.Text = $"{hold:F1}s";
                }
                catch { }
            }
            else
            {
                pnl_proximity.Visibility = Visibility.Collapsed;
            }
        }

        private void clear_props()
        {
            p_name.Text = "-";
            p_class.Text = "-";
            p_addr.Text = "-";
            p_children.Text = "-";
            p_hp.Text = "-";
            p_pos.Text = "-";
            p_flagged.Text = "-";
            btn_teleport.Visibility = Visibility.Collapsed;
            pnl_proximity.Visibility = Visibility.Collapsed;
            selected_addr = 0;
            selected_class = "";
        }

        private void BtnTeleport_Click(object sender, RoutedEventArgs e)
        {
            if (selected_addr == 0 || !Storage.IsInitialized) return;
            TeleportTo(selected_addr);
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            // full re-initialize Storage to re-walk all pointers
            if (Instance.Mem != null)
                Storage.Refresh(Instance.Mem);
            tree_loaded = false;
            last_datamodel = 0;
            last_placeid = 0;
            expanded.Clear();
            flagged.Clear();
            selected_addr = 0;
            selected_class = "";
            clear_props();
            refresh();
        }

        private void TeleportTo(long targetAddr)
        {
            try
            {
                var target = new Instance(targetAddr);
                var targetPos = target.GetPosition();

                var localChar = Storage.LocalPlayerInstance.GetCharacter();
                if (!localChar.IsValid) return;
                var hrp = localChar.FindFirstChild("HumanoidRootPart");
                if (!hrp.IsValid) return;

                long prim = Instance.Mem.ReadPtr(hrp.Address + Offsets.BasePart.Primitive);
                if (prim == 0) return;
                Instance.Mem.Write(prim + Offsets.Primitive.Position, targetPos);
            }
            catch { }
        }

        private void SldHold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (selected_addr == 0 || selected_class != "ProximityPrompt") return;
            float val = (float)e.NewValue;
            txt_hold.Text = $"{val:F1}s";
            try
            {
                Instance.Mem.Write(selected_addr + Offsets.ProximityPrompt.HoldDuration, val);
            }
            catch { }
        }

        private void refresh()
        {
            if (tree == null) return;

            // re-resolve DataModel each tick to catch game changes
            if (Instance.Mem != null && Storage.IsInitialized)
            {
                try
                {
                    long currentPlaceId = Storage.DataModelInstance.GetPlaceID();
                    if (currentPlaceId != last_placeid && last_placeid != 0)
                    {
                        // place changed — full re-init
                        Storage.Refresh(Instance.Mem);
                        tree_loaded = false;
                        last_datamodel = 0;
                        expanded.Clear();
                        flagged.Clear();
                        selected_addr = 0;
                        selected_class = "";
                        clear_props();
                    }
                    last_placeid = currentPlaceId;
                }
                catch { }
            }

            // validate tree is still readable — check first top-level node name
            if (tree_loaded && tree.Items.Count > 0)
            {
                try
                {
                    if (tree.Items[0] is TreeViewItem tvi && tvi.Tag is long addr)
                    {
                        var inst = new Instance(addr);
                        string name = inst.GetName();
                        if (string.IsNullOrEmpty(name) || !IsReadableString(name))
                        {
                            // stale data detected — force re-init and rebuild
                            if (Instance.Mem != null)
                                Storage.Refresh(Instance.Mem);
                            tree_loaded = false;
                            last_datamodel = 0;
                            expanded.Clear();
                        }
                    }
                }
                catch
                {
                    if (Instance.Mem != null)
                        Storage.Refresh(Instance.Mem);
                    tree_loaded = false;
                    last_datamodel = 0;
                    expanded.Clear();
                }
            }

            if (!Storage.IsInitialized || !Storage.DataModelInstance.IsValid)
            {
                tree_loaded = false;
                last_datamodel = 0;
                if (tree.Items.Count != 1) { tree.Items.Clear(); tree.Items.Add(new TreeViewItem { Header = "Attach to roblox first..." }); }
                return;
            }

            // detect DataModel address change
            long currentDm = Storage.DataModelInstance.Address;
            if (currentDm != last_datamodel)
            {
                tree_loaded = false;
                last_datamodel = currentDm;
                expanded.Clear();
                flagged.Clear();
                selected_addr = 0;
                selected_class = "";
                clear_props();
            }

            if (!tree_loaded)
            {
                tree.Items.Clear();
                foreach (var i in Storage.DataModelInstance.GetChildren())
                {
                    var node = make_node(i);
                    if (node != null) tree.Items.Add(node);
                }
                tree_loaded = true;
                return;
            }

            // refresh children of expanded nodes so new players/instances appear
            refresh_expanded(tree.Items);
        }

        private static bool IsReadableString(string s)
        {
            if (s.Length > 200) return false;
            foreach (char c in s)
            {
                if (c < 0x20 && c != '\t' && c != '\n' && c != '\r') return false;
                if (c > 0x7E && c < 0xA0) return false;
            }
            return true;
        }

        private void refresh_expanded(ItemCollection items)
        {
            foreach (var item in items)
            {
                if (item is not TreeViewItem tvi) continue;
                if (!tvi.IsExpanded) continue;
                if (tvi.Tag is not long addr) continue;

                // re-read children for this expanded node
                try
                {
                    var kids = new Instance(addr).GetChildren();
                    var existing = new HashSet<long>();
                    foreach (var child in tvi.Items)
                    {
                        if (child is TreeViewItem c && c.Tag is long ca)
                            existing.Add(ca);
                    }

                    // add any new children that aren't already in the tree
                    foreach (var k in kids)
                    {
                        if (!existing.Contains(k.Address))
                        {
                            var childNode = make_node(k);
                            if (childNode != null) tvi.Items.Add(childNode);
                        }
                    }

                    // remove children that no longer exist
                    var valid = new HashSet<long>(kids.Select(k => k.Address));
                    var toRemove = new List<TreeViewItem>();
                    foreach (var child in tvi.Items)
                    {
                        if (child is TreeViewItem c && c.Tag is long ca && !valid.Contains(ca))
                            toRemove.Add(c);
                    }
                    foreach (var r in toRemove) tvi.Items.Remove(r);

                    // recurse into expanded children
                    refresh_expanded(tvi.Items);
                }
                catch { }
            }
        }

        private TreeViewItem? make_node(Instance i)
        {
            string n = i.GetName()?.Trim() ?? "";
            if (string.IsNullOrEmpty(n) || n == "???" || n == "[Unnamed]" || !IsReadableString(n)) return null;

            long addr = i.Address;
            string cls = i.GetClass() ?? "";
            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            var img = new Image { Width = 14, Height = 14, Margin = new Thickness(0, 0, 5, 0), Source = get_ico(cls) };

            bool isFlagged = flagged.Contains(addr);
            var txt = new TextBlock
            {
                Text = n,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = isFlagged
                    ? new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B))
                    : (Brush)FindResource("item_grad")
            };

            stack.Children.Add(img);
            stack.Children.Add(txt);

            var tvi = new TreeViewItem { Header = stack, Tag = addr };

            // right-click context menu (Layuh-style)
            tvi.MouseRightButtonDown += (s, e) =>
            {
                e.Handled = true;
                tvi.IsSelected = true;

                var ctx = new ContextMenu { Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x14)), BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)) };

                // Name & Class header
                ctx.Items.Add(new MenuItem { Header = $"Name: {n}", Foreground = Brushes.White, IsEnabled = false });
                ctx.Items.Add(new MenuItem { Header = $"Class: {cls}", Foreground = Brushes.Gray, IsEnabled = false });
                ctx.Items.Add(new Separator());

                // Flag toggle
                bool currentlyFlagged = flagged.Contains(addr);
                var flagItem = new MenuItem { Header = currentlyFlagged ? "Unflag" : "Flag", Foreground = Brushes.White };
                flagItem.Click += (_, __) =>
                {
                    if (flagged.Contains(addr))
                        flagged.Remove(addr);
                    else
                        flagged.Add(addr);

                    // update text color
                    txt.Foreground = flagged.Contains(addr)
                        ? new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B))
                        : (Brush)FindResource("item_grad");

                    set_props();
                };
                ctx.Items.Add(flagItem);

                // Teleport (for parts)
                if (TeleportableClasses.Contains(cls))
                {
                    var tpItem = new MenuItem { Header = "Teleport", Foreground = Brushes.White };
                    tpItem.Click += (_, __) => TeleportTo(addr);
                    ctx.Items.Add(tpItem);
                }

                // ProximityPrompt hold duration
                if (cls == "ProximityPrompt")
                {
                    var promptItem = new MenuItem { Header = "Set Hold Duration to 0", Foreground = Brushes.White };
                    promptItem.Click += (_, __) =>
                    {
                        try { Instance.Mem.Write(addr + Offsets.ProximityPrompt.HoldDuration, 0f); } catch { }
                    };
                    ctx.Items.Add(promptItem);
                }

                tvi.ContextMenu = ctx;
                ctx.IsOpen = true;
            };

            try { if (i.GetChildren().Count > 0) tvi.Items.Add("..."); } catch { }

            tvi.Expanded += (s, e) =>
            {
                expanded.Add(addr);
                if (tvi.Items.Count == 1 && tvi.Items[0] is string)
                {
                    tvi.Items.Clear();
                    Task.Run(() =>
                    {
                        var kids = new Instance(addr).GetChildren();
                        Dispatcher.Invoke(() =>
                        {
                            foreach (var k in kids)
                            {
                                var childNode = make_node(k);
                                if (childNode != null) tvi.Items.Add(childNode);
                            }
                        });
                    });
                }
            };

            tvi.Collapsed += (s, e) =>
            {
                expanded.Remove(addr);
                tvi.Items.Clear();
                tvi.Items.Add("...");
            };
            return tvi;
        }

        private BitmapImage? get_ico(string cls)
        {
            if (string.IsNullOrEmpty(cls)) return null;
            if (cache.TryGetValue(cls, out var b)) return b;

            byte[]? d = cls switch
            {
                "Workspace" => icons.workspace,
                "Folder" => icons.folder,
                "Camera" => icons.camera,
                "Humanoid" => icons.humanoid,
                "Part" => icons.part,
                "Players" => icons.players,
                "MeshPart" => icons.meshpart,
                "Player" => icons.player,
                "Model" => icons.model,
                "Terrain" => icons.terrain,
                "LocalScript" => icons.localscript,
                "LocalScripts" => icons.localscripts,
                "Script" => icons.localscript,
                "ModuleScript" => icons.localscript,
                "PlayerGui" => icons.playergui,
                "Stats" => icons.stats,
                "GuiService" => icons.guiservice,
                "VideoCapture" => icons.videocapture,
                "RunService" => icons.runservice,
                "Frame" => icons.frame,
                "ContentProvider" => icons.contentprovider,
                "NonReplicated" => icons.nonreplicated,
                "StarterGear" => icons.startergear,
                "TimerDevice" => icons.timerdevice,
                "Backpack" => icons.backpack,
                "MarketplaceService" => icons.marketplaceservice,
                "SoundService" => icons.soundservice,
                "Sound" => icons.soundservice,
                "LogService" => icons.logservice,
                "StatsItem" => icons.statsitem,
                "BoolValue" => icons.boolvalue,
                "IntValue" => icons.intvalue,
                "DoubleType" => icons.doubletype,
                "Type" => icons.typeshit,
                "AncientLogo" => icons.ancientlogo,
                "Lightning" => icons.lightning,
                // Layuh mappings using closest available Foulz icons
                "Accessory" => icons.model,
                "Hat" => icons.model,
                "SpawnLocation" => icons.part,
                "UnionOperation" => icons.part,
                "ReplicatedStorage" => icons.folder,
                "ReplicatedFirst" => icons.folder,
                "StarterGui" => icons.playergui,
                "StarterPack" => icons.backpack,
                "StarterPlayer" => icons.player,
                "Chat" => icons.folder,
                "CoreGui" => icons.guiservice,
                "RemoteEvent" => icons.lightning,
                "RemoteFunction" => icons.lightning,
                "UIListLayout" => icons.frame,
                "TextLabel" => icons.frame,
                "TextButton" => icons.frame,
                "ImageLabel" => icons.frame,
                "BillboardGui" => icons.frame,
                "SurfaceGui" => icons.frame,
                "ProximityPrompt" => icons.lightning,
                _ => null
            };

            if (d == null || d.Length == 0) return null;
            try
            {
                using var ms = new MemoryStream(d);
                var img = new BitmapImage();
                img.BeginInit(); img.CacheOption = BitmapCacheOption.OnLoad; img.StreamSource = ms; img.DecodePixelWidth = 16; img.EndInit();
                img.Freeze(); cache[cls] = img; return img;
            }
            catch { return null; }
        }
    }
}