#pragma once
#define IMGUI_DEFINE_MATH_OPERATORS

#include <string>
#include <vector>
#include <map>
#include <mutex>
#include <variant>
#include "../classes/math/math.h"
#include "../../drawing/thirdparty/imgui/imgui.h"

struct lua_State;

namespace util {
    // ============================================================================
    // BRIDGE DATA STRUCTURES
    // ============================================================================
    struct LuaVector3 { float x, y, z; };
    struct LuaCFrame { float m[12]; };
    struct LuaColor3 { float r, g, b; };
    struct LuaUDim { float scale; int offset; };
    struct LuaUDim2 { LuaUDim x; LuaUDim y; };

    // ============================================================================
    // LUAVM - Main Virtual Machine Class
    // ============================================================================
    class LuaVM {
    public:
        static LuaVM& get();

        // Core VM operations
        void init();
        void execute_script(const std::string& script);
        void step();
        void restart();

        // Logging system
        std::vector<std::string> get_logs();
        size_t get_log_count();
        void clear_logs();
        void log_print(const std::string& str);

        // Drawing system
        void render_drawings(ImDrawList* draw_list);

        // Service accessors
        void push_game_service(lua_State* L);
        void push_workspace_service(lua_State* L);

        // Thread management
        lua_State* state();
        int create_coroutine(lua_State** out_thread);
        void schedule_thread(lua_State* thread, int registry_ref, double wake_time_sec, int pending_args = 0, bool at_front = false);
        int ensure_thread_registry_ref(lua_State* thread);

        // ====================================================================
        // VIRTUAL ROBLOX INSTANCES - Emulate Roblox Instance behavior
        // ====================================================================
        
        struct VirtualInstance {
            std::string ClassName;
            std::string Name;
            VirtualInstance* Parent = nullptr;
            std::vector<VirtualInstance*> Children;
            uint64_t RealAddress = 0; // Bridge to actual Roblox instance (0 if purely virtual)
            bool Visible = true;
            bool Archivable = true;
            std::map<std::string, std::string> Tags;
            std::map<std::string, std::variant<bool, double, std::string>> Attributes;

            virtual ~VirtualInstance();
            virtual void render(ImDrawList* dl, ImVec2 parent_pos, ImVec2 parent_size);

            ImVec2 calculate_pos(LuaUDim2 pos, ImVec2 parent_pos, ImVec2 parent_size);
            ImVec2 calculate_size(LuaUDim2 size, ImVec2 parent_size);
            
            VirtualInstance* FindFirstChild(const std::string& name);
            VirtualInstance* FindFirstChildOfClass(const std::string& className);
            VirtualInstance* FindFirstAncestor(const std::string& name);
            std::vector<VirtualInstance*> GetChildren();
            std::vector<VirtualInstance*> GetDescendants();
            bool IsA(const std::string& className);
            bool IsDescendantOf(VirtualInstance* ancestor);
            bool IsAncestorOf(VirtualInstance* descendant);
        };

        // ====================================================================
        // UI COMPONENTS
        // ====================================================================
        
        struct VirtualUIComponent : VirtualInstance {
            LuaUDim2 Position = { {0,0}, {0,0} };
            LuaUDim2 Size = { {0,100}, {0,100} };
            LuaUDim2 AnchorPoint = { {0,0}, {0,0} };
            ImColor BackgroundColor3 = ImColor(255,255,255);
            float BackgroundTransparency = 0.0f;
            ImColor BorderColor3 = ImColor(27, 42, 53);
            int BorderSizePixel = 1;
            int ZIndex = 1;
            int LayoutOrder = 0;
            float Rotation = 0.0f;
            bool ClipsDescendants = false;
            bool Active = false;
            bool Selectable = false;
            bool AutomaticSize = false;
            
            void render_bg(ImDrawList* dl, ImVec2 p, ImVec2 s);
        };
        
        struct VirtualScreenGui : VirtualInstance {
            bool Enabled = true;
            int DisplayOrder = 0;
            bool IgnoreGuiInset = false;
            bool ResetOnSpawn = true;
            int ZIndexBehavior = 0;
             
            VirtualScreenGui();
            void render(ImDrawList* dl, ImVec2 parent_pos, ImVec2 parent_size) override;
        };
        
        struct VirtualBillboardGui : VirtualInstance {
            bool Enabled = true;
            float Size_X_Scale = 0;
            int Size_X_Offset = 100;
            float Size_Y_Scale = 0;
            int Size_Y_Offset = 100;
            float StudsOffset_X = 0;
            float StudsOffset_Y = 0;
            float StudsOffset_Z = 0;
            bool AlwaysOnTop = false;
            float MaxDistance = 1e10f;
            
            VirtualBillboardGui();
        };
        
        struct VirtualFrame : VirtualUIComponent {
            int Style = 0;
            VirtualFrame();
            void render(ImDrawList* dl, ImVec2 parent_pos, ImVec2 parent_size) override;
        };
        
        struct VirtualScrollingFrame : VirtualUIComponent {
            float CanvasSize_X_Scale = 0;
            int CanvasSize_X_Offset = 0;
            float CanvasSize_Y_Scale = 2;
            int CanvasSize_Y_Offset = 0;
            float CanvasPosition_X = 0;
            float CanvasPosition_Y = 0;
            ImColor ScrollBarColor3 = ImColor(200, 200, 200);
            float ScrollBarThickness = 12;
            bool ScrollingEnabled = true;
            
            VirtualScrollingFrame();
            void render(ImDrawList* dl, ImVec2 parent_pos, ImVec2 parent_size) override;
        };
        
        struct VirtualTextLabel : VirtualUIComponent {
            std::string Text = "Label";
            ImColor TextColor3 = ImColor(0,0,0);
            float TextSize = 14.0f;
            bool TextScaled = false;
            bool TextWrapped = false;
            float TextTransparency = 0.0f;
            int TextXAlignment = 1;
            int TextYAlignment = 1;
            bool RichText = false;
            float LineHeight = 1.0f;
            int MaxVisibleGraphemes = -1;
            std::string Font = "SourceSans";
            
            VirtualTextLabel();
            void render(ImDrawList* dl, ImVec2 parent_pos, ImVec2 parent_size) override;
        };
        
        struct VirtualTextButton : VirtualTextLabel {
            bool AutoButtonColor = true;
            int Style = 0;
            bool Modal = false;
            bool Selected = false;
            
            VirtualTextButton();
        };
        
        struct VirtualTextBox : VirtualTextLabel {
            std::string PlaceholderText = "";
            ImColor PlaceholderColor3 = ImColor(178, 178, 178);
            bool ClearTextOnFocus = true;
            bool MultiLine = false;
            bool TextEditable = true;
            int CursorPosition = -1;
            int SelectionStart = -1;
            
            VirtualTextBox();
        };
        
        struct VirtualImageLabel : VirtualUIComponent {
            std::string Image = "";
            ImColor ImageColor3 = ImColor(255, 255, 255);
            float ImageTransparency = 0.0f;
            int ScaleType = 0;
            
            VirtualImageLabel();
            void render(ImDrawList* dl, ImVec2 parent_pos, ImVec2 parent_size) override;
        };
        
        struct VirtualImageButton : VirtualImageLabel {
            std::string HoverImage = "";
            std::string PressedImage = "";
            bool AutoButtonColor = true;
            
            VirtualImageButton();
        };
        
        struct VirtualUIListLayout : VirtualInstance {
            int FillDirection = 1;
            int HorizontalAlignment = 0;
            int VerticalAlignment = 0;
            int SortOrder = 0;
            float Padding_Scale = 0;
            int Padding_Offset = 0;
            bool Wraps = false;
            
            VirtualUIListLayout();
        };
        
        struct VirtualUIGridLayout : VirtualInstance {
            int FillDirection = 0;
            int FillDirectionMaxCells = 0;
            float CellSize_X_Scale = 0;
            int CellSize_X_Offset = 100;
            float CellSize_Y_Scale = 0;
            int CellSize_Y_Offset = 100;
            float CellPadding_X_Scale = 0;
            int CellPadding_X_Offset = 5;
            float CellPadding_Y_Scale = 0;
            int CellPadding_Y_Offset = 5;
            
            VirtualUIGridLayout();
        };

        // ====================================================================
        // 3D INSTANCES
        // ====================================================================
        
        struct VirtualFolder : VirtualInstance {
            VirtualFolder();
        };

        struct VirtualBasePart : VirtualInstance {
            math::Vector3 Size = {4, 1, 2};
            math::Vector3 Position = {0, 0, 0};
            math::Vector3 Orientation = {0, 0, 0};
            math::Vector3 Velocity = {0, 0, 0};
            math::Vector3 AssemblyLinearVelocity = {0, 0, 0};
            math::Vector3 AssemblyAngularVelocity = {0, 0, 0};
            float Transparency = 0.0f;
            float Reflectance = 0.0f;
            ImColor Color = ImColor(163, 162, 165);
            int Material = 256;
            int BrickColor = 194;
            bool CanCollide = true;
            bool CanTouch = true;
            bool CanQuery = true;
            bool Anchored = false;
            bool Massless = false;
            float Mass = 1.0f;
            int CollisionGroupId = 0;
            bool CastShadow = true;
            
            VirtualBasePart();
        };

        struct VirtualPart : VirtualBasePart {
            int Shape = 1;
            VirtualPart();
        };
        
        struct VirtualMeshPart : VirtualBasePart {
            std::string MeshId = "";
            std::string TextureID = "";
            math::Vector3 MeshSize = {1, 1, 1};
            
            VirtualMeshPart();
        };
        
        struct VirtualSpawnLocation : VirtualBasePart {
            bool AllowTeamChangeOnTouch = false;
            int Duration = 10;
            bool Enabled = true;
            bool Neutral = true;
            
            VirtualSpawnLocation();
        };
        
        struct VirtualSeat : VirtualBasePart {
            bool Disabled = false;
            VirtualInstance* Occupant = nullptr;
            
            VirtualSeat();
        };

        struct VirtualModel : VirtualInstance {
            VirtualInstance* PrimaryPart = nullptr;
            math::Vector3 WorldPivot = {0, 0, 0};
            
            VirtualModel();
            math::Vector3 GetPivot();
            void SetPrimaryPartCFrame(const math::Vector3& pos);
        };

        // ====================================================================
        // CHARACTER INSTANCES
        // ====================================================================
        
        struct VirtualHumanoid : VirtualInstance {
            float Health = 100.0f;
            float MaxHealth = 100.0f;
            float WalkSpeed = 16.0f;
            float JumpPower = 50.0f;
            float JumpHeight = 7.2f;
            bool AutoJumpEnabled = true;
            bool AutoRotate = true;
            int HumanoidStateType = 8;
            int RigType = 0;
            bool RequiresNeck = true;
            float HipHeight = 2.0f;
            bool UseJumpPower = true;
            std::string DisplayName = "";
            int NameOcclusion = 2;
            int HealthDisplayType = 0;
            float NameDisplayDistance = 100;
            float HealthDisplayDistance = 100;
            
            VirtualHumanoid();
        };
        
        struct VirtualAccessory : VirtualInstance {
            std::string AttachmentPoint = "";
            VirtualInstance* Handle = nullptr;
            
            VirtualAccessory();
        };

        // ====================================================================
        // VALUE INSTANCES
        // ====================================================================
        
        struct VirtualStringValue : VirtualInstance {
            std::string Value;
            VirtualStringValue();
        };

        struct VirtualIntValue : VirtualInstance {
            int Value = 0;
            VirtualIntValue();
        };

        struct VirtualBoolValue : VirtualInstance {
            bool Value = false;
            VirtualBoolValue();
        };

        struct VirtualNumberValue : VirtualInstance {
            double Value = 0.0;
            VirtualNumberValue();
        };
        
        struct VirtualObjectValue : VirtualInstance {
            VirtualInstance* Value = nullptr;
            VirtualObjectValue();
        };
        
        struct VirtualColor3Value : VirtualInstance {
            ImColor Value = ImColor(255, 255, 255);
            VirtualColor3Value();
        };
        
        struct VirtualVector3Value : VirtualInstance {
            math::Vector3 Value = {0, 0, 0};
            VirtualVector3Value();
        };
        
        struct VirtualCFrameValue : VirtualInstance {
            LuaCFrame Value = {};
            VirtualCFrameValue();
        };
        
        struct VirtualRayValue : VirtualInstance {
            math::Vector3 Origin = {0, 0, 0};
            math::Vector3 Direction = {0, 0, -1};
            VirtualRayValue();
        };

        // ====================================================================
        // SERVICE INSTANCES
        // ====================================================================
        
        struct VirtualService : VirtualInstance {
            VirtualService(const std::string& serviceName);
        };
        
        struct VirtualCamera : VirtualInstance {
            math::Vector3 CFrame_Position = {0, 20, 20};
            math::Vector3 CFrame_LookVector = {0, -0.5f, -0.866f};
            float FieldOfView = 70.0f;
            int CameraType = 0;
            VirtualInstance* CameraSubject = nullptr;
            float Focus_X = 0, Focus_Y = 0, Focus_Z = 0;
            math::Vector3 ViewportSize = {1920, 1080, 0};
            float NearPlaneZ = 0.5f;
            float MaxAxisFieldOfView = 70.0f;
            bool HeadLocked = true;
            float HeadScale = 1.0f;
            
            VirtualCamera();
        };
        
        struct VirtualBindableEvent : VirtualInstance {
            VirtualBindableEvent();
        };
        
        struct VirtualBindableFunction : VirtualInstance {
            VirtualBindableFunction();
        };
        
        struct VirtualRemoteEvent : VirtualInstance {
            VirtualRemoteEvent();
        };
        
        struct VirtualRemoteFunction : VirtualInstance {
            VirtualRemoteFunction();
        };

        // ====================================================================
        // DRAWING CLASSES
        // ====================================================================

        struct DrawingBase {
            bool visible = true;
            int zindex = 1;
            float transparency = 1.0f;
            ImColor color = ImColor(255, 255, 255);
            virtual ~DrawingBase() = default;
            virtual void render(ImDrawList* dl) = 0;
            virtual void remove() { delete this; }
        };

        struct DrawingLine : DrawingBase {
            ImVec2 from{0,0}, to{0,0};
            float thickness = 1.0f;
            void render(ImDrawList* dl) override;
        };

        struct DrawingText : DrawingBase {
            std::string text;
            ImVec2 position{0,0};
            float size = 13.0f;
            bool center = false;
            bool outline = false;
            ImColor outline_color = ImColor(0,0,0);
            void render(ImDrawList* dl) override;
        };

        struct DrawingSquare : DrawingBase {
            ImVec2 position{0,0};
            ImVec2 size{0,0};
            bool filled = false;
            float thickness = 1.0f;
            void render(ImDrawList* dl) override;
        };

        struct DrawingCircle : DrawingBase {
            ImVec2 position{0,0};
            float radius = 0.0f;
            bool filled = false;
            float thickness = 1.0f;
            int num_sides = 0;
            void render(ImDrawList* dl) override;
        };

        struct DrawingTriangle : DrawingBase {
            ImVec2 p1{0,0}, p2{0,0}, p3{0,0};
            bool filled = false;
            float thickness = 1.0f;
            void render(ImDrawList* dl) override;
        };

        void add_drawing(DrawingBase* d);
        void remove_drawing(DrawingBase* d);
        
        void add_virtual_root(VirtualScreenGui* gui);
        void remove_virtual_root(VirtualScreenGui* gui);

        ImVec2 mouse_pos = ImVec2(0.0f, 0.0f);
        std::mutex mouse_mutex;

        // Simulated Physics
        struct SimulatedObject {
            uintptr_t primitive_addr;
            uintptr_t instance_addr;
            math::Vector3 velocity;
        };
        std::map<uintptr_t, SimulatedObject> m_simulated_physics;
        std::recursive_mutex m_physics_mutex;

        void set_simulated_velocity(uintptr_t instance_addr, uintptr_t primitive_addr, const math::Vector3& velocity);

    private:
        LuaVM();
        ~LuaVM();

        LuaVM(const LuaVM&) = delete;
        LuaVM& operator=(const LuaVM&) = delete;

        struct lua_State* L = nullptr;
        std::recursive_mutex vm_mutex;
        std::vector<std::string> logs;
        std::recursive_mutex log_mutex;

        struct ScriptThread {
            struct lua_State* thread = nullptr;
            int registry_ref = -1;
            double wake_time_sec = 0.0;
            int pending_args = 0;
        };

        std::vector<ScriptThread> script_threads;
        std::recursive_mutex script_mutex;
        
        std::vector<DrawingBase*> drawings;
        std::vector<VirtualScreenGui*> virtual_roots;
        std::recursive_mutex drawing_mutex;
    };
}
