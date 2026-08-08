using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using FoulzExternal.SDK;
using FoulzExternal.storage;
using SInstance = FoulzExternal.SDK.Instance;

namespace IMGUI
{
    // ────────────────────────────────────────────────────────────────────────
    //  ExplorerWindow — Yerba-styled ImGui Dex Explorer. Faithful port of the
    //  geeg lad explorer (ExplorerTree.h + explorer_window.cpp): DataModel
    //  root, service sorting, auto-open Workspace, lazy child loading, search,
    //  and a right-click context menu.
    // ────────────────────────────────────────────────────────────────────────
    public static class ExplorerWindow
    {
        public static bool Open;

        private static bool posInited;
        private static Vector2 pos = new(160f, 100f);
        private static Vector2 size = new(320f, 480f);

        private static bool dragActive;
        private static Vector2 dragStartMouse;
        private static Vector2 dragStartPos;

        private static string search = "";
        private static long selectedAddr;
        private static long ctxAddr;
        private static string ctxName = "";
        private static string ctxClass = "";

        private class Node
        {
            public long Address;
            public string Name = "";
            public string Class = "";
            public bool HasKids;
            public bool Loaded;
            public bool Open;
            public List<Node> Children = new();
        }

        private static Node root = new();
        private static long rootAddr;

        // ── Service sort order (matches geeg lad ExplorerTree.h) ────────────
        private static readonly string[] ServiceOrder = {
            "Workspace", "Players", "Lighting", "MaterialService", "ReplicatedFirst",
            "ReplicatedStorage", "ServerStorage", "ServerScriptService", "StarterGui",
            "StarterPack", "StarterPlayer", "SoundService", "Chat", "TextChatService",
            "Teams", "TeleportService", "TweenService", "RunService", "UserInputService",
            "GuiService", "HttpService", "MarketplaceService", "InsertService", "Debris"
        };

        private static int ServiceRank(string cls)
        {
            for (int i = 0; i < ServiceOrder.Length; ++i)
                if (cls == ServiceOrder[i]) return i;
            return 1000;
        }

        private static int ServiceLess(Node a, Node b)
        {
            int ra = ServiceRank(a.Class);
            int rb = ServiceRank(b.Class);
            if (ra != rb) return ra.CompareTo(rb);
            return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
        }

        private static void SortChildren(Node n)
        {
            if (n.Class != "DataModel") return;
            n.Children.Sort(ServiceLess);
        }

        private static void LoadChildren(Node n)
        {
            if (n.Loaded) return;
            n.Loaded = true;
            n.Children.Clear();
            try
            {
                var inst = new SInstance(n.Address);
                foreach (var c in inst.GetChildren())
                {
                    string name = c.GetName()?.Trim() ?? "";
                    if (string.IsNullOrEmpty(name) || name == "???" || name == "[Unnamed]") continue;
                    string cls = c.GetClass() ?? "";
                    bool hasKids = false;
                    try { hasKids = c.GetChildren().Count > 0; } catch { }
                    n.Children.Add(new Node
                    {
                        Address = c.Address,
                        Name = name,
                        Class = cls,
                        HasKids = hasKids
                    });
                }
            }
            catch { }
            n.HasKids = n.Children.Count > 0;
            SortChildren(n);

            // Auto-open Workspace under DataModel (matches geeg lad)
            if (n.Class == "DataModel")
            {
                foreach (var c in n.Children)
                {
                    if (c.Class == "Workspace")
                    {
                        c.Open = true;
                        LoadChildren(c);
                        break;
                    }
                }
            }
        }

        private static void EnsureRoot()
        {
            long dm = Storage.IsInitialized ? Storage.DataModelInstance.Address : 0;
            if (dm != 0 && dm != rootAddr)
            {
                rootAddr = dm;
                root = new Node { Address = dm, Class = "DataModel", Name = "game", Open = true };
            }
            else if (dm == 0)
            {
                rootAddr = 0;
                root = new Node();
            }
        }

        private static string BuildPath(long address)
        {
            var parts = new List<string>();
            long cur = address;
            int guard = 64;
            while (cur != 0 && guard-- > 0)
            {
                try
                {
                    var node = new SInstance(cur);
                    string nm = node.GetName();
                    if (string.IsNullOrEmpty(nm)) nm = "?";
                    parts.Add(nm);
                    if (cur == rootAddr) break;
                    // No GetParent in Ardvark SDK — stop at root
                    break;
                }
                catch { break; }
            }
            parts.Reverse();
            return string.Join(".", parts);
        }

        private static void RenderNode(ImDrawListPtr dl, Node node, int depth, float availW, ref float y, float bottom)
        {
            const float rowH = 22f;
            if (y + rowH > bottom) return;

            bool hasKids = node.Loaded ? node.Children.Count > 0 : node.HasKids;
            bool isSelected = selectedAddr == node.Address;

            var rowMin = new Vector2(pos.X + 4f, y);
            var rowMax = new Vector2(pos.X + availW - 4f, y + rowH);

            bool hovered = YerbaWidgets.IsMouseHoveringRect(rowMin, rowMax);
            if (hovered && !isSelected)
                dl.AddRectFilled(rowMin, rowMax, YerbaColors.WithAlpha(YerbaColors.TextActive, 0.06f));
            if (isSelected)
                dl.AddRect(new Vector2(rowMin.X + 2f, rowMin.Y + 2f), new Vector2(rowMax.X - 2f, rowMax.Y - 2f),
                    YerbaColors.WithAlpha(YerbaColors.TextActive, 0.35f));

            float indent = 15f;
            float arrowX = rowMin.X + depth * indent + 2f;

            if (hasKids)
            {
                var arrowCenter = new Vector2(arrowX + 4f, (rowMin.Y + rowMax.Y) * 0.5f);
                if (node.Open)
                {
                    dl.AddTriangleFilled(arrowCenter + new Vector2(-3f, -2f), arrowCenter + new Vector2(3f, -2f), arrowCenter + new Vector2(0f, 3f), YerbaColors.TextIdle);
                }
                else
                {
                    dl.AddTriangleFilled(arrowCenter + new Vector2(-2f, -3f), arrowCenter + new Vector2(-2f, 3f), arrowCenter + new Vector2(3f, 0f), YerbaColors.TextIdle);
                }
            }

            float textX = rowMin.X + depth * indent + (hasKids ? 18f : 6f);
            var textSize = ImGui.CalcTextSize(node.Name);
            dl.AddText(new Vector2(textX, (rowMin.Y + rowMax.Y) * 0.5f - textSize.Y * 0.5f),
                isSelected ? YerbaColors.TextActive : YerbaColors.WithAlpha(YerbaColors.TextActive, 0.82f), node.Name);

            if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                selectedAddr = node.Address;
                bool onArrow = hasKids && ImGui.GetIO().MousePos.X >= arrowX && ImGui.GetIO().MousePos.X < arrowX + 16f;
                if (onArrow)
                {
                    if (!node.Loaded) LoadChildren(node);
                    node.Open = !node.Open;
                }
            }

            if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                selectedAddr = node.Address;
                ctxAddr = node.Address;
                ctxName = node.Name;
                ctxClass = node.Class;
                ImGui.OpenPopup("##ex_ctx");
            }

            y += rowH;

            if (node.Open)
            {
                if (!node.Loaded) LoadChildren(node);
                foreach (var child in node.Children)
                    RenderNode(dl, child, depth + 1, availW, ref y, bottom);
            }
        }

        private static void RenderSearchResults(ImDrawListPtr dl, Node node, int depth, float availW, ref float y, float bottom)
        {
            const float rowH = 22f;
            if (y + rowH > bottom) return;

            if (node.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var rowMin = new Vector2(pos.X + 4f, y);
                var rowMax = new Vector2(pos.X + availW - 4f, y + rowH);
                bool hovered = YerbaWidgets.IsMouseHoveringRect(rowMin, rowMax);
                bool isSelected = selectedAddr == node.Address;

                if (hovered && !isSelected)
                    dl.AddRectFilled(rowMin, rowMax, YerbaColors.WithAlpha(YerbaColors.TextActive, 0.06f));
                if (isSelected)
                    dl.AddRect(new Vector2(rowMin.X + 2f, rowMin.Y + 2f), new Vector2(rowMax.X - 2f, rowMax.Y - 2f),
                        YerbaColors.WithAlpha(YerbaColors.TextActive, 0.35f));

                var textSize = ImGui.CalcTextSize(node.Name);
                dl.AddText(new Vector2(rowMin.X + 8f, (rowMin.Y + rowMax.Y) * 0.5f - textSize.Y * 0.5f),
                    isSelected ? YerbaColors.TextActive : YerbaColors.WithAlpha(YerbaColors.TextActive, 0.82f), node.Name);

                if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    selectedAddr = node.Address;
                if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                {
                    selectedAddr = node.Address;
                    ctxAddr = node.Address;
                    ctxName = node.Name;
                    ctxClass = node.Class;
                    ImGui.OpenPopup("##ex_ctx");
                }

                y += rowH;
            }

            if (!node.Loaded) LoadChildren(node);
            foreach (var child in node.Children)
                RenderSearchResults(dl, child, depth + 1, availW, ref y, bottom);
        }

        public static void Render()
        {
            if (!Open) return;

            var io = ImGui.GetIO();

            if (!posInited)
            {
                posInited = true;
                pos = new Vector2(
                    (io.DisplaySize.X - size.X) * 0.5f + 160f,
                    (io.DisplaySize.Y - size.Y) * 0.5f - 40f);
            }

            bool overHeader = !YerbaWidgets.IsMouseHoveringAnyControl() &&
                io.MousePos.X >= pos.X && io.MousePos.X <= pos.X + size.X &&
                io.MousePos.Y >= pos.Y && io.MousePos.Y <= pos.Y + 26f;

            if (!dragActive && io.MouseClicked[0] && overHeader)
            {
                dragActive = true;
                dragStartMouse = io.MousePos;
                dragStartPos = pos;
            }

            if (dragActive)
            {
                if (io.MouseDown[0])
                    pos = Vector2.Max(dragStartPos + (io.MousePos - dragStartMouse), Vector2.Zero);
                else
                    dragActive = false;
            }

            ImGui.SetNextWindowPos(pos, ImGuiCond.Always);
            ImGui.SetNextWindowSize(size, ImGuiCond.Always);

            var flags = ImGuiWindowFlags.NoDecoration |
                ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoCollapse |
                ImGuiWindowFlags.NoSavedSettings |
                ImGuiWindowFlags.NoFocusOnAppearing |
                ImGuiWindowFlags.NoBackground;

            ImGui.Begin("##explorer_window", flags);
            var dl = ImGui.GetWindowDrawList();

            var min = pos;
            var max = pos + size;

            dl.AddRectFilled(min, max, YerbaColors.BodyBg, YerbaLayout.CornerR);

            var headerMin = min;
            var headerMax = new Vector2(max.X, min.Y + 26f);
            MenuUI.DrawHeaderBackground(dl, headerMin, headerMax, YerbaLayout.CornerR);

            var titleSize = ImGui.CalcTextSize("explorer");
            dl.AddText(new Vector2((headerMin.X + headerMax.X) * 0.5f - titleSize.X * 0.5f,
                (headerMin.Y + headerMax.Y) * 0.5f - titleSize.Y * 0.5f),
                YerbaColors.TextActive, "explorer");

            var closeMin = new Vector2(headerMax.X - 28f, headerMin.Y + 3f);
            var closeMax = new Vector2(headerMax.X - 4f, headerMax.Y - 3f);
            bool closeHover = YerbaWidgets.IsMouseHoveringRect(closeMin, closeMax);
            if (closeHover)
                dl.AddRectFilled(closeMin, closeMax, YerbaColors.KeybindBgActive, 4f);
            var xSize = ImGui.CalcTextSize("X");
            dl.AddText(new Vector2((closeMin.X + closeMax.X) * 0.5f - xSize.X * 0.5f,
                (closeMin.Y + closeMax.Y) * 0.5f - xSize.Y * 0.5f),
                closeHover ? YerbaColors.TextActive : YerbaColors.TextIdle, "X");
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && closeHover)
                Open = false;

            var sepMin = new Vector2(min.X, headerMax.Y);
            var sepMax = new Vector2(max.X, headerMax.Y + YerbaLayout.SeparatorH);
            MenuUI.DrawGradientSeparator(dl, sepMin, sepMax, true);

            var bodyMin = new Vector2(min.X, sepMax.Y);
            var bodyMax = max;
            dl.AddRectFilled(bodyMin, bodyMax, YerbaColors.BodyBg, YerbaLayout.CornerR, ImDrawFlags.RoundCornersBottom);
            MenuUI.DrawDotGrid(dl, bodyMin, bodyMax);

            float pad = 6f;
            float searchH = 24f;
            var searchMin = new Vector2(bodyMin.X + pad, bodyMin.Y + pad);
            var searchMax = new Vector2(bodyMax.X - pad, searchMin.Y + searchH);

            dl.AddRectFilled(searchMin, searchMax, YerbaColors.SearchBg, YerbaLayout.SearchRound);
            YerbaWidgets.DrawFieldOutline(dl, searchMin, searchMax, YerbaColors.SearchBorder, YerbaLayout.SearchRound, 1f);

            ImGui.SetCursorScreenPos(searchMin);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.Border, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.Text, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.TextSelectedBg, Vector4.Zero);
            ImGui.SetNextItemWidth(searchMax.X - searchMin.X - 8f);
            string searchBuf = search;
            if (ImGui.InputText("##explorer_search", ref searchBuf, 128))
                search = searchBuf;
            ImGui.PopStyleColor(6);
            ImGui.PopStyleVar();

            var searchTextSize = ImGui.CalcTextSize(search);
            dl.AddText(new Vector2(searchMin.X + 8f, (searchMin.Y + searchMax.Y) * 0.5f - searchTextSize.Y * 0.5f),
                YerbaColors.TextActive, search);

            EnsureRoot();
            if (root.Address != 0 && !root.Loaded)
                LoadChildren(root);

            float treeTop = searchMax.Y + pad;
            float treeBottom = bodyMax.Y - pad;
            float availW = bodyMax.X - bodyMin.X - pad * 2f;

            dl.PushClipRect(new Vector2(bodyMin.X, treeTop), new Vector2(bodyMax.X, treeBottom), true);

            float y = treeTop;
            if (root.Address != 0)
            {
                if (string.IsNullOrEmpty(search))
                {
                    foreach (var child in root.Children)
                        RenderNode(dl, child, 0, availW, ref y, treeBottom);
                }
                else
                {
                    RenderSearchResults(dl, root, 0, availW, ref y, treeBottom);
                }
            }
            else
            {
                var msgSize = ImGui.CalcTextSize("not attached / no datamodel");
                dl.AddText(new Vector2((bodyMin.X + bodyMax.X) * 0.5f - msgSize.X * 0.5f, treeTop + 10f),
                    YerbaColors.TextIdle, "not attached / no datamodel");
            }

            dl.PopClipRect();

            if (ImGui.BeginPopup("##ex_ctx"))
            {
                if (ctxAddr != 0)
                {
                    if (ImGui.MenuItem("copy name"))
                        ImGui.SetClipboardText(ctxName);
                    if (ImGui.MenuItem("copy class"))
                        ImGui.SetClipboardText(ctxClass);
                    if (ImGui.MenuItem("copy path"))
                        ImGui.SetClipboardText(BuildPath(ctxAddr));
                    if (ImGui.MenuItem("copy address"))
                        ImGui.SetClipboardText($"0x{ctxAddr:X}");
                    if (ImGui.MenuItem("teleport to position"))
                        TeleportTo(ctxAddr);
                }
                ImGui.EndPopup();
            }

            dl.AddRect(min, max, YerbaColors.WithAlpha(YerbaColors.IceBlue, YerbaLayout.OutlineOpacity),
                YerbaLayout.CornerR, ImDrawFlags.None, YerbaLayout.IceBorder);

            ImGui.End();
        }

        private static void TeleportTo(long targetAddr)
        {
            try
            {
                var target = new SInstance(targetAddr);
                var targetPos = target.GetPosition();

                var localChar = Storage.LocalPlayerInstance.GetCharacter();
                if (!localChar.IsValid) return;
                var hrp = localChar.FindFirstChild("HumanoidRootPart");
                if (!hrp.IsValid) return;

                long prim = SInstance.Mem.ReadPtr(hrp.Address + Offsets.BasePart.Primitive);
                if (prim == 0) return;
                SInstance.Mem.Write(prim + Offsets.Primitive.Position, targetPos);
            }
            catch { }
        }
    }
}