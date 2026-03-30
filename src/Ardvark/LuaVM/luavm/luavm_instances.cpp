// ============================================================================
// LuaVM Instances - Instance API and VirtualInstance implementations
// ============================================================================

#include "luavm_common.h"
#include <new>
#include <functional>
#include <cstdio>
#include <set>
#include <wininet.h>
#pragma comment(lib, "wininet.lib")


namespace util {
    // VirtualInstance implementations
    LuaVM::VirtualInstance::~VirtualInstance() {
        for (auto* child : Children) delete child;
    }

    void LuaVM::VirtualInstance::render(ImDrawList*, ImVec2, ImVec2) {}

    ImVec2 LuaVM::VirtualInstance::calculate_pos(LuaUDim2 pos, ImVec2 parent_pos, ImVec2 parent_size) {
        return ImVec2(
            parent_pos.x + pos.x.scale * parent_size.x + pos.x.offset,
            parent_pos.y + pos.y.scale * parent_size.y + pos.y.offset
        );
    }

    ImVec2 LuaVM::VirtualInstance::calculate_size(LuaUDim2 size, ImVec2 parent_size) {
        return ImVec2(
            size.x.scale * parent_size.x + size.x.offset,
            size.y.scale * parent_size.y + size.y.offset
        );
    }

    LuaVM::VirtualInstance* LuaVM::VirtualInstance::FindFirstChild(const std::string& name) {
        for (auto* child : Children) {
            if (child->Name == name) return child;
        }
        return nullptr;
    }

    LuaVM::VirtualInstance* LuaVM::VirtualInstance::FindFirstChildOfClass(const std::string& className) {
        for (auto* child : Children) {
            if (child->ClassName == className) return child;
        }
        return nullptr;
    }

    LuaVM::VirtualInstance* LuaVM::VirtualInstance::FindFirstAncestor(const std::string& name) {
        VirtualInstance* p = Parent;
        while (p) {
            if (p->Name == name) return p;
            p = p->Parent;
        }
        return nullptr;
    }

    std::vector<LuaVM::VirtualInstance*> LuaVM::VirtualInstance::GetChildren() {
        if (RealAddress != 0) {
            roblox::instance real_inst(RealAddress);
            auto real_children = real_inst.get_children();
            
            // Build set of valid real addresses
            std::set<uint64_t> valid_addresses;
            for (const auto& rc : real_children) {
                if (rc.address != 0) {
                    valid_addresses.insert(rc.address);
                }
            }
            
            // Remove stale cached children (those whose RealAddress is no longer valid)
            for (auto it = Children.begin(); it != Children.end(); ) {
                if ((*it)->RealAddress != 0 && valid_addresses.find((*it)->RealAddress) == valid_addresses.end()) {
                    delete *it;
                    it = Children.erase(it);
                } else {
                    ++it;
                }
            }
            
            // Add new children that aren't cached yet
            for (const auto& rc : real_children) {
                if (rc.address == 0) continue;
                
                // Check if already cached
                bool found = false;
                for (auto* vc : Children) {
                    if (vc->RealAddress == rc.address) {
                        found = true;
                        break;
                    }
                }
                
                if (!found) {
                    // Create wrapper
                    auto* new_child = new LuaVM::VirtualInstance();
                    new_child->Name = const_cast<roblox::instance&>(rc).get_name();
                    new_child->ClassName = const_cast<roblox::instance&>(rc).get_class_name();
                    new_child->RealAddress = rc.address;
                    new_child->Parent = this;
                    
                   if (new_child->ClassName == "Model") {
                        auto* m = new LuaVM::VirtualModel();
                        m->Name = new_child->Name;
                        m->ClassName = new_child->ClassName;
                        m->Parent = new_child->Parent;
                        m->RealAddress = new_child->RealAddress;
                        delete new_child;
                        new_child = m;
                    } else if (new_child->ClassName == "Part" || new_child->ClassName == "MeshPart" || new_child->ClassName == "UnionOperation"
                        || new_child->ClassName == "WedgePart" || new_child->ClassName == "CornerWedgePart" || new_child->ClassName == "TrussPart" 
                        || new_child->ClassName == "Seat" || new_child->ClassName == "VehicleSeat" || new_child->ClassName == "SpawnLocation") {
                        auto* p = new LuaVM::VirtualBasePart();
                        p->Name = new_child->Name;
                        p->ClassName = new_child->ClassName;
                        p->Parent = new_child->Parent;
                        p->RealAddress = new_child->RealAddress;
                        delete new_child;
                        new_child = p;
                    } else if (new_child->ClassName == "Camera") {
                        auto* c = new LuaVM::VirtualCamera();
                        c->Name = new_child->Name;
                        c->ClassName = new_child->ClassName;
                        c->Parent = new_child->Parent;
                        c->RealAddress = new_child->RealAddress;
                        delete new_child;
                        new_child = c;
                    }
                    
                    Children.push_back(new_child);
                }
            }
        }
        return Children;
    }

    std::vector<LuaVM::VirtualInstance*> LuaVM::VirtualInstance::GetDescendants() {
        std::vector<VirtualInstance*> result;
        // Use GetChildren() to ensure lazy loading
        std::function<void(VirtualInstance*)> collect = [&](VirtualInstance* inst) {
            for (auto* child : inst->GetChildren()) {
                result.push_back(child);
                collect(child);
            }
        };
        collect(this);
        return result;
    }

    bool LuaVM::VirtualInstance::IsA(const std::string& className) {
    if (ClassName == className) return true;
    if (className == "Instance") return true;
    
    // BasePart hierarchy check
    if (className == "BasePart" || className == "PVInstance") {
         if (ClassName == "Part" || ClassName == "MeshPart" || ClassName == "CornerWedgePart" 
             || ClassName == "TrussPart" || ClassName == "WedgePart" || ClassName == "Seat" || ClassName == "VehicleSeat"
             || ClassName == "SpawnLocation") return true;
    }
    
    // Part hierarchy check (SpawnLocation, Seat, etc inherits from Part)
    if (className == "Part") {
        if (ClassName == "SpawnLocation" || ClassName == "Seat" || ClassName == "VehicleSeat" 
            || ClassName == "Platform" || ClassName == "SkateboardPlatform") return true;
    }
    
    // GuiObject hierarchy
    if (className == "GuiObject" || className == "UIComponent" || className == "GuiBase2d") {
        if (ClassName == "Frame" || ClassName == "TextLabel" || ClassName == "TextButton" 
            || ClassName == "TextBox" || ClassName == "ImageLabel" || ClassName == "ImageButton" 
            || ClassName == "ScrollingFrame") return true;
    }
    
    // Model hierarchy
    if (className == "PVInstance") {
        if (ClassName == "Model") return true;
    }

    // ValueBase hierarchy
    if (className == "ValueBase") {
        if (ClassName.find("Value") != std::string::npos && ClassName != "ValueBase") return true;
    }
    
    return false;
}

    bool LuaVM::VirtualInstance::IsDescendantOf(VirtualInstance* ancestor) {
        VirtualInstance* p = Parent;
        while (p) {
            if (p == ancestor) return true;
            p = p->Parent;
        }
        return false;
    }

    bool LuaVM::VirtualInstance::IsAncestorOf(VirtualInstance* descendant) {
        return descendant->IsDescendantOf(this);
    }

    // VirtualUIComponent
    void LuaVM::VirtualUIComponent::render_bg(ImDrawList* dl, ImVec2 p, ImVec2 s) {
        if (BackgroundTransparency >= 1.0f) return;
        ImU32 col = BackgroundColor3;
        col = (col & 0x00FFFFFF) | (((int)((1.0f - BackgroundTransparency) * 255)) << 24);
        dl->AddRectFilled(p, p + s, col);
    }

    // Virtual type constructors
    LuaVM::VirtualScreenGui::VirtualScreenGui() { ClassName = "ScreenGui"; Name = "ScreenGui"; }
    void LuaVM::VirtualScreenGui::render(ImDrawList* dl, ImVec2, ImVec2 parent_size) {
        if (!Enabled) return;
        for (auto* child : Children) child->render(dl, ImVec2(0, 0), parent_size);
    }

    LuaVM::VirtualBillboardGui::VirtualBillboardGui() { ClassName = "BillboardGui"; Name = "BillboardGui"; }
    LuaVM::VirtualFrame::VirtualFrame() { ClassName = "Frame"; Name = "Frame"; }
    void LuaVM::VirtualFrame::render(ImDrawList* dl, ImVec2 parent_pos, ImVec2 parent_size) {
        if (!Visible) return;
        ImVec2 pos = calculate_pos(Position, parent_pos, parent_size);
        ImVec2 size = calculate_size(Size, parent_size);
        render_bg(dl, pos, size);
        for (auto* child : Children) child->render(dl, pos, size);
    }

    LuaVM::VirtualScrollingFrame::VirtualScrollingFrame() { ClassName = "ScrollingFrame"; Name = "ScrollingFrame"; }
    void LuaVM::VirtualScrollingFrame::render(ImDrawList* dl, ImVec2 parent_pos, ImVec2 parent_size) {
        if (!Visible) return;
        ImVec2 pos = calculate_pos(Position, parent_pos, parent_size);
        ImVec2 size = calculate_size(Size, parent_size);
        render_bg(dl, pos, size);
        for (auto* child : Children) child->render(dl, pos, size);
    }

    LuaVM::VirtualTextLabel::VirtualTextLabel() { ClassName = "TextLabel"; Name = "TextLabel"; }
    void LuaVM::VirtualTextLabel::render(ImDrawList* dl, ImVec2 parent_pos, ImVec2 parent_size) {
        if (!Visible) return;
        ImVec2 pos = calculate_pos(Position, parent_pos, parent_size);
        ImVec2 size = calculate_size(Size, parent_size);
        render_bg(dl, pos, size);
        ImU32 textCol = ImGui::ColorConvertFloat4ToU32(ImVec4(TextColor3.Value.x, TextColor3.Value.y, TextColor3.Value.z, 1.0f - TextTransparency));
        dl->AddText(pos, textCol, Text.c_str());
    }

    LuaVM::VirtualTextButton::VirtualTextButton() { ClassName = "TextButton"; Name = "TextButton"; }
    LuaVM::VirtualTextBox::VirtualTextBox() { ClassName = "TextBox"; Name = "TextBox"; }
    LuaVM::VirtualImageLabel::VirtualImageLabel() { ClassName = "ImageLabel"; Name = "ImageLabel"; }
    void LuaVM::VirtualImageLabel::render(ImDrawList* dl, ImVec2 parent_pos, ImVec2 parent_size) {
        if (!Visible) return;
        ImVec2 pos = calculate_pos(Position, parent_pos, parent_size);
        ImVec2 size = calculate_size(Size, parent_size);
        render_bg(dl, pos, size);
    }

    LuaVM::VirtualImageButton::VirtualImageButton() { ClassName = "ImageButton"; Name = "ImageButton"; }
    LuaVM::VirtualUIListLayout::VirtualUIListLayout() { ClassName = "UIListLayout"; Name = "UIListLayout"; }
    LuaVM::VirtualUIGridLayout::VirtualUIGridLayout() { ClassName = "UIGridLayout"; Name = "UIGridLayout"; }
    LuaVM::VirtualFolder::VirtualFolder() { ClassName = "Folder"; Name = "Folder"; }
    LuaVM::VirtualBasePart::VirtualBasePart() { ClassName = "BasePart"; Name = "Part"; }
    LuaVM::VirtualPart::VirtualPart() { ClassName = "Part"; Name = "Part"; }
    LuaVM::VirtualMeshPart::VirtualMeshPart() { ClassName = "MeshPart"; Name = "MeshPart"; }
    LuaVM::VirtualSpawnLocation::VirtualSpawnLocation() { ClassName = "SpawnLocation"; Name = "SpawnLocation"; }
    LuaVM::VirtualSeat::VirtualSeat() { ClassName = "Seat"; Name = "Seat"; }
    LuaVM::VirtualModel::VirtualModel() { ClassName = "Model"; Name = "Model"; }
    math::Vector3 LuaVM::VirtualModel::GetPivot() {
        if (PrimaryPart) {
            auto* bp = dynamic_cast<VirtualBasePart*>(PrimaryPart);
            if (bp) return bp->Position;
        }
        return WorldPivot;
    }
    void LuaVM::VirtualModel::SetPrimaryPartCFrame(const math::Vector3& pos) {
        if (PrimaryPart) {
            auto* bp = dynamic_cast<VirtualBasePart*>(PrimaryPart);
            if (bp) bp->Position = pos;
        }
    }

    LuaVM::VirtualHumanoid::VirtualHumanoid() { ClassName = "Humanoid"; Name = "Humanoid"; }
    LuaVM::VirtualAccessory::VirtualAccessory() { ClassName = "Accessory"; Name = "Accessory"; }
    LuaVM::VirtualStringValue::VirtualStringValue() { ClassName = "StringValue"; Name = "Value"; }
    LuaVM::VirtualIntValue::VirtualIntValue() { ClassName = "IntValue"; Name = "Value"; }
    LuaVM::VirtualBoolValue::VirtualBoolValue() { ClassName = "BoolValue"; Name = "Value"; }
    LuaVM::VirtualNumberValue::VirtualNumberValue() { ClassName = "NumberValue"; Name = "Value"; }
    LuaVM::VirtualObjectValue::VirtualObjectValue() { ClassName = "ObjectValue"; Name = "Value"; }
    LuaVM::VirtualColor3Value::VirtualColor3Value() { ClassName = "Color3Value"; Name = "Value"; }
    LuaVM::VirtualVector3Value::VirtualVector3Value() { ClassName = "Vector3Value"; Name = "Value"; }
    LuaVM::VirtualCFrameValue::VirtualCFrameValue() { ClassName = "CFrameValue"; Name = "Value"; }
    LuaVM::VirtualRayValue::VirtualRayValue() { ClassName = "RayValue"; Name = "Value"; }
    LuaVM::VirtualService::VirtualService(const std::string& serviceName) { ClassName = serviceName; Name = serviceName; }
    LuaVM::VirtualCamera::VirtualCamera() { ClassName = "Camera"; Name = "Camera"; }
    LuaVM::VirtualBindableEvent::VirtualBindableEvent() { ClassName = "BindableEvent"; Name = "BindableEvent"; }
    LuaVM::VirtualBindableFunction::VirtualBindableFunction() { ClassName = "BindableFunction"; Name = "BindableFunction"; }
    LuaVM::VirtualRemoteEvent::VirtualRemoteEvent() { ClassName = "RemoteEvent"; Name = "RemoteEvent"; }
    LuaVM::VirtualRemoteFunction::VirtualRemoteFunction() { ClassName = "RemoteFunction"; Name = "RemoteFunction"; }

    // push_roblox_instance_userdata
    void push_roblox_instance_userdata(lua_State* L, const roblox::instance& inst) {
        LuaInstance* ud = (LuaInstance*)lua_newuserdata(L, sizeof(LuaInstance));
        new (ud) LuaInstance();
        ud->inst = inst;
        ud->v_inst = nullptr;
        luaL_getmetatable(L, "RobloxInstance");
        lua_setmetatable(L, -2);
    }

    int L_Instance_new(lua_State* L) {
        const char* className = luaL_checkstring(L, 1);
        
        LuaVM::VirtualInstance* inst = nullptr;
        
        if (strcmp(className, "ScreenGui") == 0) inst = new LuaVM::VirtualScreenGui();
        else if (strcmp(className, "Frame") == 0) inst = new LuaVM::VirtualFrame();
        else if (strcmp(className, "TextLabel") == 0) inst = new LuaVM::VirtualTextLabel();
        else if (strcmp(className, "TextButton") == 0) inst = new LuaVM::VirtualTextButton();
        else if (strcmp(className, "TextBox") == 0) inst = new LuaVM::VirtualTextBox();
        else if (strcmp(className, "ImageLabel") == 0) inst = new LuaVM::VirtualImageLabel();
        else if (strcmp(className, "ImageButton") == 0) inst = new LuaVM::VirtualImageButton();
        else if (strcmp(className, "ScrollingFrame") == 0) inst = new LuaVM::VirtualScrollingFrame();
        else if (strcmp(className, "Folder") == 0) inst = new LuaVM::VirtualFolder();
        else if (strcmp(className, "Part") == 0) inst = new LuaVM::VirtualPart();
        else if (strcmp(className, "Model") == 0) inst = new LuaVM::VirtualModel();
        else if (strcmp(className, "StringValue") == 0) inst = new LuaVM::VirtualStringValue();
        else if (strcmp(className, "IntValue") == 0) inst = new LuaVM::VirtualIntValue();
        else if (strcmp(className, "BoolValue") == 0) inst = new LuaVM::VirtualBoolValue();
        else if (strcmp(className, "NumberValue") == 0) inst = new LuaVM::VirtualNumberValue();
        else if (strcmp(className, "ObjectValue") == 0) inst = new LuaVM::VirtualObjectValue();
        else if (strcmp(className, "CFrameValue") == 0) inst = new LuaVM::VirtualCFrameValue();
        else if (strcmp(className, "Vector3Value") == 0) inst = new LuaVM::VirtualVector3Value();
        else if (strcmp(className, "Color3Value") == 0) inst = new LuaVM::VirtualColor3Value();
        else if (strcmp(className, "RayValue") == 0) inst = new LuaVM::VirtualRayValue();
        else if (strcmp(className, "BindableEvent") == 0) inst = new LuaVM::VirtualBindableEvent();
        else if (strcmp(className, "BindableFunction") == 0) inst = new LuaVM::VirtualBindableFunction();
        else if (strcmp(className, "RemoteEvent") == 0) inst = new LuaVM::VirtualRemoteEvent();
        else if (strcmp(className, "RemoteFunction") == 0) inst = new LuaVM::VirtualRemoteFunction();
        else if (strcmp(className, "Humanoid") == 0) inst = new LuaVM::VirtualHumanoid();
        else if (strcmp(className, "Camera") == 0) inst = new LuaVM::VirtualCamera();
        else if (strcmp(className, "MeshPart") == 0) inst = new LuaVM::VirtualMeshPart();
        else if (strcmp(className, "Seat") == 0) inst = new LuaVM::VirtualSeat();
        else if (strcmp(className, "Accessory") == 0) inst = new LuaVM::VirtualAccessory();
        else if (strcmp(className, "DataModel") == 0) inst = new LuaVM::VirtualInstance(); // Generic for DataModel (handled separately usually)
        else {
            inst = new LuaVM::VirtualInstance();
            inst->ClassName = className;
            inst->Name = className;
        }
        
        LuaInstance* ud = (LuaInstance*)lua_newuserdata(L, sizeof(LuaInstance));
        new (ud) LuaInstance();
        ud->inst = roblox::instance();
        ud->v_inst = inst;
        luaL_getmetatable(L, "RobloxInstance");
        lua_setmetatable(L, -2);
        
        return 1;
    }

    int L_HttpGet(lua_State* L) {
        const char* url = luaL_checkstring(L, 2); // Arg 1 is self (game), arg 2 is URL
        
        if (!url) {
            lua_pushstring(L, "");
            return 1;
        }
        
        // HTTP GET using WinINet
        HINTERNET hInternet = InternetOpenA("Nift/1.0", INTERNET_OPEN_TYPE_DIRECT, nullptr, nullptr, 0);
        if (!hInternet) {
            lua_pushstring(L, "");
            return 1;
        }
        
        HINTERNET hUrl = InternetOpenUrlA(hInternet, url, nullptr, 0, INTERNET_FLAG_RELOAD, 0);
        if (!hUrl) {
            InternetCloseHandle(hInternet);
            lua_pushstring(L, "");
            return 1;
        }
        
        std::string response;
        char buffer[4096];
        DWORD bytesRead;
        while (InternetReadFile(hUrl, buffer, sizeof(buffer) - 1, &bytesRead) && bytesRead > 0) {
            buffer[bytesRead] = '\0';
            response += buffer;
        }
        
        InternetCloseHandle(hUrl);
        InternetCloseHandle(hInternet);
        
        lua_pushstring(L, response.c_str());
        return 1;
    }

    int L_Instance_index(lua_State* L) {
        LuaInstance* ud = (LuaInstance*)lua_touserdata(L, 1);
        if (!ud) return 0;
        
        const char* key = lua_tostring(L, 2);
        if (!key) return 0;
        
        // Handle virtual instances
        if (ud->v_inst) {
            LuaVM::VirtualInstance* v = ud->v_inst;
            
            // Base Instance properties
            if (strcmp(key, "Name") == 0) {
                if (v->RealAddress != 0) {
                     std::string n = roblox::instance(v->RealAddress).get_name();
                     if (!n.empty()) v->Name = n;
                }
                lua_pushstring(L, v->Name.c_str()); 
                return 1; 
            }
            if (strcmp(key, "ClassName") == 0) { lua_pushstring(L, v->ClassName.c_str()); return 1; }
            if (strcmp(key, "Address") == 0) {
                lua_pushnumber(L, (double)v->RealAddress);
                return 1;
            }
            if (strcmp(key, "Parent") == 0) {
                if (v->Parent) {
                    LuaInstance* pud = (LuaInstance*)lua_newuserdata(L, sizeof(LuaInstance));
                    new (pud) LuaInstance();
                    pud->v_inst = v->Parent;
                    luaL_getmetatable(L, "RobloxInstance");
                    lua_setmetatable(L, -2);
                } else if (v->RealAddress != 0) {
                     // Universal bridge for Parent
                     roblox::instance rp = roblox::instance(v->RealAddress).read_parent();
                     if (rp.address != 0) {
                         LuaVM::VirtualInstance* new_parent = new LuaVM::VirtualInstance();
                         new_parent->RealAddress = rp.address;
                         new_parent->Name = rp.get_name();
                         new_parent->ClassName = rp.get_class_name();
                         
                         v->Parent = new_parent; 
                         
                         LuaInstance* pud = (LuaInstance*)lua_newuserdata(L, sizeof(LuaInstance));
                         new (pud) LuaInstance();
                         pud->v_inst = new_parent;
                         luaL_getmetatable(L, "RobloxInstance");
                         lua_setmetatable(L, -2);
                     } else {
                         lua_pushnil(L);
                     }
                } else lua_pushnil(L);
                return 1;
            }
            
            // DataModel properties
            if (v->ClassName == "DataModel") {
                if (strcmp(key, "PlaceId") == 0) { lua_pushnumber(L, 0); return 1; }
                if (strcmp(key, "GameId") == 0) { lua_pushnumber(L, 0); return 1; }
                if (strcmp(key, "JobId") == 0) { lua_pushstring(L, "00000000-0000-0000-0000-000000000000"); return 1; }
                if (strcmp(key, "HttpGet") == 0) { lua_pushcfunction(L, L_HttpGet, "HttpGet"); return 1; }
            }
            
            // Workspace properties
            if (v->ClassName == "Workspace") {
                if (strcmp(key, "CurrentCamera") == 0) {
                    vm_globals::ensure_stub_environment();
                    if (vm_globals::stub_camera) {
                        LuaInstance* cud = (LuaInstance*)lua_newuserdata(L, sizeof(LuaInstance));
                        new (cud) LuaInstance();
                        cud->v_inst = vm_globals::stub_camera;
                        luaL_getmetatable(L, "RobloxInstance");
                        lua_setmetatable(L, -2);
                        return 1;
                    }
                    lua_pushnil(L);
                    return 1;
                }
            }

            // Player properties
            if (v->ClassName == "Player") {
                if (strcmp(key, "Character") == 0) {
                    if (v->RealAddress != 0) {
                        roblox::instance character = roblox::instance(v->RealAddress).model_instance();
                        if (character.address != 0) {
                            LuaVM::VirtualModel* v_char = new LuaVM::VirtualModel();
                            v_char->Name = character.get_name();
                            v_char->ClassName = "Model"; // Character is a Model
                            v_char->RealAddress = character.address;
                            v_char->Parent = v->Parent; // Usually workspace, but logically parented to player for some scripts or just needs a valid pointer

                            LuaInstance* cud = (LuaInstance*)lua_newuserdata(L, sizeof(LuaInstance));
                            new (cud) LuaInstance();
                            cud->v_inst = v_char;
                            luaL_getmetatable(L, "RobloxInstance");
                            lua_setmetatable(L, -2);
                            return 1;
                        }
                    }
                    lua_pushnil(L);
                    return 1;
                }
            }
            
            // Players properties
            if (v->ClassName == "Players") {
                 if (strcmp(key, "LocalPlayer") == 0) {
                     if (util::vm_globals::stub_localplayer) {
                         // Always refresh the RealAddress from globals to ensure it's up to date
                         if (globals::instances::localplayer.address != 0) {
                             util::vm_globals::stub_localplayer->RealAddress = globals::instances::localplayer.address;
                         }


                         LuaInstance* pud = (LuaInstance*)lua_newuserdata(L, sizeof(LuaInstance));
                         new (pud) LuaInstance();
                         pud->v_inst = util::vm_globals::stub_localplayer;
                         luaL_getmetatable(L, "RobloxInstance");
                         lua_setmetatable(L, -2);
                     } else lua_pushnil(L);
                     return 1;
                 }
            }

            // Mouse properties
            if (v->ClassName == "Mouse") {
                if (strcmp(key, "Hit") == 0) {
                    LuaCFrame cf = {};
                    cf.m[3] = 1; cf.m[7] = 1; cf.m[11] = 1; // Identity
                    push_cframe_userdata(L, cf);
                    return 1;
                }
                if (strcmp(key, "X") == 0) { 
                    POINT p; GetCursorPos(&p);
                    lua_pushinteger(L, p.x); 
                    return 1; 
                }
                if (strcmp(key, "Y") == 0) { 
                    POINT p; GetCursorPos(&p);
                    lua_pushinteger(L, p.y); 
                    return 1; 
                }
                if (strcmp(key, "Target") == 0) { lua_pushnil(L); return 1; }
            }
            
            // Camera properties
            if (auto* cam = dynamic_cast<LuaVM::VirtualCamera*>(v)) {
                if (strcmp(key, "ViewportSize") == 0) {
                    push_vector2_userdata(L, cam->ViewportSize.x, cam->ViewportSize.y);
                    return 1;
                }
                if (strcmp(key, "FieldOfView") == 0) { lua_pushnumber(L, cam->FieldOfView); return 1; }
                if (strcmp(key, "Position") == 0) {
                    push_vector3_userdata(L, cam->CFrame_Position.x, cam->CFrame_Position.y, cam->CFrame_Position.z);
                    return 1;
                }
            }
            
            // Humanoid properties
            if (auto* hum = dynamic_cast<LuaVM::VirtualHumanoid*>(v)) {
                if (strcmp(key, "Health") == 0) { lua_pushnumber(L, hum->Health); return 1; }
                if (strcmp(key, "MaxHealth") == 0) { lua_pushnumber(L, hum->MaxHealth); return 1; }
                if (strcmp(key, "WalkSpeed") == 0) { lua_pushnumber(L, hum->WalkSpeed); return 1; }
                if (strcmp(key, "JumpPower") == 0) { lua_pushnumber(L, hum->JumpPower); return 1; }
            }
            
            // Model properties
            if (auto* model = dynamic_cast<LuaVM::VirtualModel*>(v)) {
                if (strcmp(key, "PrimaryPart") == 0) {
                    if (model->PrimaryPart) {
                        LuaInstance* pud = (LuaInstance*)lua_newuserdata(L, sizeof(LuaInstance));
                        new (pud) LuaInstance();
                        pud->v_inst = model->PrimaryPart;
                        luaL_getmetatable(L, "RobloxInstance");
                        lua_setmetatable(L, -2);
                    } else lua_pushnil(L);
                    return 1;
                }
            }
            
            // BasePart properties
            if (auto* part = dynamic_cast<LuaVM::VirtualBasePart*>(v)) {
                if (strcmp(key, "Size") == 0) {
                    math::Vector3 sz = part->Size;
                    if (v->RealAddress != 0) {
                        sz = roblox::instance(v->RealAddress).get_part_size();
                        part->Size = sz; // Update cache
                    }
                    LuaVector3 v3 = { sz.x, sz.y, sz.z };
                    push_vector3_userdata(L, v3.x, v3.y, v3.z);
                    return 1;
                }
                if (strcmp(key, "Position") == 0) {

                    math::Vector3 pos = part->Position;
                    if (v->RealAddress != 0) {
                        pos = roblox::instance(v->RealAddress).get_pos();
                        part->Position = pos; // Update cache
                    }
                    LuaVector3 v3 = { pos.x, pos.y, pos.z };
                    push_vector3_userdata(L, v3.x, v3.y, v3.z);
                    return 1;
                }
                if (strcmp(key, "CFrame") == 0) {
                    if (v->RealAddress != 0) {
                         CFrame real_cf = roblox::instance(v->RealAddress).read_cframe();
                         LuaCFrame cf;
                         // Map roblox::CFrame (3x3 rot + pos)
                         cf.m[0] = real_cf.rotation_matrix.data[0]; cf.m[1] = real_cf.rotation_matrix.data[1]; cf.m[2] = real_cf.rotation_matrix.data[2];
                         cf.m[3] = real_cf.position.x;
                         cf.m[4] = real_cf.rotation_matrix.data[3]; cf.m[5] = real_cf.rotation_matrix.data[4]; cf.m[6] = real_cf.rotation_matrix.data[5];
                         cf.m[7] = real_cf.position.y;
                         cf.m[8] = real_cf.rotation_matrix.data[6]; cf.m[9] = real_cf.rotation_matrix.data[7]; cf.m[10] = real_cf.rotation_matrix.data[8];
                         cf.m[11] = real_cf.position.z;
                         push_cframe_userdata(L, cf);
                         return 1;
                    }
                    LuaCFrame cf = {};
                    cf.m[0] = 1; cf.m[5] = 1; cf.m[10] = 1;
                    cf.m[3] = part->Position.x; cf.m[7] = part->Position.y; cf.m[11] = part->Position.z;
                    push_cframe_userdata(L, cf);
                    return 1;
                }
                if (strcmp(key, "Transparency") == 0) { lua_pushnumber(L, part->Transparency); return 1; }
                if (strcmp(key, "CanCollide") == 0) { lua_pushboolean(L, part->CanCollide); return 1; }
                if (strcmp(key, "Velocity") == 0) { push_vector3_userdata(L, part->Velocity.x, part->Velocity.y, part->Velocity.z); return 1; }
                if (strcmp(key, "AssemblyLinearVelocity") == 0) { push_vector3_userdata(L, part->AssemblyLinearVelocity.x, part->AssemblyLinearVelocity.y, part->AssemblyLinearVelocity.z); return 1; }
                if (strcmp(key, "Color") == 0) { push_color3_userdata(L, part->Color.Value.x, part->Color.Value.y, part->Color.Value.z); return 1; }

            }
            
            // MeshPart properties
            if (auto* mesh = dynamic_cast<LuaVM::VirtualMeshPart*>(v)) {
                if (strcmp(key, "MeshId") == 0) { lua_pushstring(L, mesh->MeshId.c_str()); return 1; }
                if (strcmp(key, "TextureID") == 0 || strcmp(key, "TextureId") == 0) { lua_pushstring(L, mesh->TextureID.c_str()); return 1; }
            }
            
            // ValueBase properties - read from real Roblox memory if available
            if (strcmp(key, "Value") == 0 && v->RealAddress != 0) {
                roblox::instance real_inst(v->RealAddress);
                // IntValue, NumberValue use read_int_value / read_double_value
                if (v->ClassName == "IntValue") {
                    lua_pushinteger(L, real_inst.read_int_value());
                    return 1;
                } else if (v->ClassName == "NumberValue") {
                    lua_pushnumber(L, real_inst.read_double_value());
                    return 1;
                } else if (v->ClassName == "BoolValue") {
                    lua_pushboolean(L, real_inst.read_bool_value());
                    return 1;
                }
            }
            // Fallback to virtual values for purely virtual instances
            if (auto* sv = dynamic_cast<LuaVM::VirtualStringValue*>(v)) {
                if (strcmp(key, "Value") == 0) { lua_pushstring(L, sv->Value.c_str()); return 1; }
            }
            if (auto* iv = dynamic_cast<LuaVM::VirtualIntValue*>(v)) {
                if (strcmp(key, "Value") == 0) { lua_pushinteger(L, iv->Value); return 1; }
            }
            if (auto* nv = dynamic_cast<LuaVM::VirtualNumberValue*>(v)) {
                if (strcmp(key, "Value") == 0) { lua_pushnumber(L, nv->Value); return 1; }
            }
            if (auto* bv = dynamic_cast<LuaVM::VirtualBoolValue*>(v)) {
                if (strcmp(key, "Value") == 0) { lua_pushboolean(L, bv->Value); return 1; }
            }
            
            // TextLabel properties - read from real Roblox memory if available
            if (strcmp(key, "Text") == 0 && v->RealAddress != 0) {
                // Read Text from Roblox memory using GuiObjectText offset
                char buffer[256] = {};
                uintptr_t textAddr = v->RealAddress + offsets::GuiObjectText;
                for (int i = 0; i < 255; ++i) {
                    char c = read<char>(textAddr + i);
                    if (c == 0) break;
                    buffer[i] = c;
                }
                lua_pushstring(L, buffer);
                return 1;
            }
            // Fallback for virtual TextLabel
            if (auto* tl = dynamic_cast<LuaVM::VirtualTextLabel*>(v)) {
                if (strcmp(key, "Text") == 0) { lua_pushstring(L, tl->Text.c_str()); return 1; }
                if (strcmp(key, "TextSize") == 0) { lua_pushnumber(L, tl->TextSize); return 1; }
            }
            
            // GuiObject/UIComponent properties - simple fallback values
            // NOTE: AbsolutePosition/AbsoluteSize calculation not implemented - offsets unavailable
            if (strcmp(key, "AbsoluteSize") == 0) { 
                push_vector2_userdata(L, 1920.0f, 1080.0f); // Screen size fallback
                return 1; 
            }
            if (strcmp(key, "AbsolutePosition") == 0) { 
                push_vector2_userdata(L, 0.0f, 0.0f); // Top-left fallback
                return 1; 
            }
            
            // Check children by name
            LuaVM::VirtualInstance* found_child = nullptr;
            
            // First pass: Check cache
            for (auto* child : v->Children) {
                if (child->Name == key) { found_child = child; break; }
            }
            
            // Second pass: Refresh cache and check again
            if (!found_child) {
                v->GetChildren(); // Update cache from real memory
                for (auto* child : v->Children) {
                    if (child->Name == key) { found_child = child; break; }
                }
            }

            if (found_child) {

                LuaInstance* cud = (LuaInstance*)lua_newuserdata(L, sizeof(LuaInstance));
                new (cud) LuaInstance();
                cud->v_inst = found_child;
                luaL_getmetatable(L, "RobloxInstance");
                lua_setmetatable(L, -2);
                return 1;
            }
            
            // if (strcmp(key, "HumanoidRootPart") == 0) LuaVM::get().log_print("[DEBUG] Failed to find HumanoidRootPart via index");
            return 0;
        }
        
        // Handle real instances
        if (ud->inst.is_valid()) {
            if (strcmp(key, "Name") == 0) { lua_pushstring(L, ud->inst.get_name().c_str()); return 1; }
            if (strcmp(key, "ClassName") == 0) { lua_pushstring(L, ud->inst.get_class_name().c_str()); return 1; }
            if (strcmp(key, "Position") == 0) {
                Vector3 pos = ud->inst.get_pos();
                push_vector3_userdata(L, pos.x, pos.y, pos.z);
                return 1;
            }
            if (strcmp(key, "Parent") == 0) {
                roblox::instance parent = ud->inst.read_parent();
                if (parent.is_valid()) push_roblox_instance_userdata(L, parent);
                else lua_pushnil(L);
                return 1;
            }
            if (strcmp(key, "Address") == 0) {
                lua_pushnumber(L, (double)ud->inst.address);
                return 1;
            }
            
            // Try FindFirstChild
            roblox::instance child = ud->inst.findfirstchild(key);
            if (child.is_valid()) {
                push_roblox_instance_userdata(L, child);
                return 1;
            }
        }
        
        return 0;
    }

    int L_Instance_newindex(lua_State* L) {
        LuaInstance* ud = (LuaInstance*)lua_touserdata(L, 1);
        if (!ud) return 0;
        
        const char* key = lua_tostring(L, 2);
        if (!key) return 0;
        
        if (ud->v_inst) {
            LuaVM::VirtualInstance* v = ud->v_inst;
            if (strcmp(key, "Name") == 0) { v->Name = lua_tostring(L, 3); return 0; }
            if (strcmp(key, "Parent") == 0) {
                if (lua_isnil(L, 3)) {
                    if (v->Parent) {
                        auto& children = v->Parent->Children;
                        children.erase(std::remove(children.begin(), children.end(), v), children.end());
                        v->Parent = nullptr;
                    }
                    return 0;
                }
                LuaInstance* pud = (LuaInstance*)lua_touserdata(L, 3);
                if (pud && pud->v_inst) {
                    if (v->Parent) {
                        auto& children = v->Parent->Children;
                        children.erase(std::remove(children.begin(), children.end(), v), children.end());
                    }
                    v->Parent = pud->v_inst;
                    pud->v_inst->Children.push_back(v);
                }
                return 0;
            }
            
            // Humanoid properties
            if (auto* hum = dynamic_cast<LuaVM::VirtualHumanoid*>(v)) {
                if (strcmp(key, "Health") == 0) { hum->Health = (float)lua_tonumber(L, 3); return 0; }
                if (strcmp(key, "MaxHealth") == 0) { hum->MaxHealth = (float)lua_tonumber(L, 3); return 0; }
                if (strcmp(key, "WalkSpeed") == 0) { hum->WalkSpeed = (float)lua_tonumber(L, 3); return 0; }
                if (strcmp(key, "JumpPower") == 0) { hum->JumpPower = (float)lua_tonumber(L, 3); return 0; }
            }
            
            // Model properties
            if (auto* model = dynamic_cast<LuaVM::VirtualModel*>(v)) {
                if (strcmp(key, "PrimaryPart") == 0) {
                    if (lua_isnil(L, 3)) {
                        model->PrimaryPart = nullptr;
                    } else {
                        LuaInstance* pud = (LuaInstance*)lua_touserdata(L, 3);
                        if (pud && pud->v_inst) model->PrimaryPart = pud->v_inst;
                    }
                    return 0;
                }
            }
            
            // BasePart properties
            if (auto* part = dynamic_cast<LuaVM::VirtualBasePart*>(v)) {
                if (strcmp(key, "Size") == 0) {
                    Vector3* vec = try_get_vector3_userdata(L, 3);
                    if (vec) { part->Size = math::Vector3(vec->x, vec->y, vec->z); }
                    return 0;
                }
                if (strcmp(key, "Position") == 0) {
                    Vector3* vec = try_get_vector3_userdata(L, 3);
                    if (vec) { part->Position = math::Vector3(vec->x, vec->y, vec->z); }
                    return 0;
                }
                if (strcmp(key, "Transparency") == 0) { part->Transparency = (float)lua_tonumber(L, 3); return 0; }
                if (strcmp(key, "CanCollide") == 0) { part->CanCollide = lua_toboolean(L, 3); return 0; }
                if (strcmp(key, "Color") == 0) {
                    LuaColor3* col = try_get_color3_userdata(L, 3);
                    if (col) { part->Color = ImColor(col->r, col->g, col->b); }
                    return 0;
                }
            }
            
            // ValueBase properties
            if (auto* sv = dynamic_cast<LuaVM::VirtualStringValue*>(v)) {
                if (strcmp(key, "Value") == 0) { sv->Value = lua_tostring(L, 3); return 0; }
            }
            if (auto* iv = dynamic_cast<LuaVM::VirtualIntValue*>(v)) {
                if (strcmp(key, "Value") == 0) { iv->Value = (int)lua_tointeger(L, 3); return 0; }
            }
            if (auto* nv = dynamic_cast<LuaVM::VirtualNumberValue*>(v)) {
                if (strcmp(key, "Value") == 0) { nv->Value = lua_tonumber(L, 3); return 0; }
            }
            if (auto* bv = dynamic_cast<LuaVM::VirtualBoolValue*>(v)) {
                if (strcmp(key, "Value") == 0) { bv->Value = lua_toboolean(L, 3); return 0; }
            }
            
            // TextLabel properties
            if (auto* tl = dynamic_cast<LuaVM::VirtualTextLabel*>(v)) {
                if (strcmp(key, "Text") == 0) { tl->Text = lua_tostring(L, 3); return 0; }
                if (strcmp(key, "TextSize") == 0) { tl->TextSize = (float)lua_tonumber(L, 3); return 0; }
            }
        }
        
        return 0;
    }

    int L_Instance_namecall(lua_State* L) {
        LuaInstance* ud = (LuaInstance*)lua_touserdata(L, 1);
        if (!ud) return 0;
        
        const char* method = lua_namecallatom(L, nullptr);
        if (!method) return 0;
        
        // Handle virtual instances
        if (ud->v_inst) {
            LuaVM::VirtualInstance* v = ud->v_inst;
            
            if (strcmp(method, "FindFirstChild") == 0) {
                const char* name = luaL_checkstring(L, 2);
                
                // Use universal GetChildren() bridge to find the child
                // This ensures we use the robust factory logic and consistent caching
                auto children = v->GetChildren();
                
                for (auto* child : children) {
                    if (child->Name == name) {
                         LuaInstance* cud = (LuaInstance*)lua_newuserdata(L, sizeof(LuaInstance));
                         new (cud) LuaInstance();
                         cud->v_inst = child;
                         luaL_getmetatable(L, "RobloxInstance");
                         lua_setmetatable(L, -2);
                         return 1;
                    }
                }
                
                lua_pushnil(L);
                return 1;
            }
            if (strcmp(method, "GetChildren") == 0) {
                // Logic moved to VirtualInstance::GetChildren() for universal access
                std::vector<LuaVM::VirtualInstance*> children = v->GetChildren();

                lua_newtable(L);
                int i = 1;
                for (auto* child : children) {
                    LuaInstance* cud = (LuaInstance*)lua_newuserdata(L, sizeof(LuaInstance));
                    new (cud) LuaInstance();
                    cud->v_inst = child;
                    luaL_getmetatable(L, "RobloxInstance");
                    lua_setmetatable(L, -2);
                    lua_rawseti(L, -2, i++);
                }
                return 1;
            }
            if (strcmp(method, "GetMouse") == 0) {
                LuaVM::VirtualInstance* mouse = new LuaVM::VirtualInstance();
                mouse->ClassName = "Mouse";
                mouse->Name = "Mouse";
                
                LuaInstance* mud = (LuaInstance*)lua_newuserdata(L, sizeof(LuaInstance));
                new (mud) LuaInstance();
                mud->v_inst = mouse;
                luaL_getmetatable(L, "RobloxInstance");
                lua_setmetatable(L, -2);
                return 1;
            }
            if (strcmp(method, "IsA") == 0) {
                const char* className = luaL_checkstring(L, 2);
                lua_pushboolean(L, v->IsA(className));
                return 1;
            }
            if (strcmp(method, "Destroy") == 0) {
                if (v->Parent) {
                    auto& children = v->Parent->Children;
                    children.erase(std::remove(children.begin(), children.end(), v), children.end());
                    v->Parent = nullptr;
                }
                return 0;
            }
            if (strcmp(method, "GetFullName") == 0) {
                std::string fullName = v->Name;
                LuaVM::VirtualInstance* p = v->Parent;
                while (p) {
                    fullName = p->Name + "." + fullName;
                    p = p->Parent;
                }
                lua_pushstring(L, fullName.c_str());
                return 1;
            }
            if (strcmp(method, "GetDescendants") == 0) {
                std::vector<LuaVM::VirtualInstance*> descendants = v->GetDescendants();
                lua_newtable(L);
                int i = 1;
                for (auto* desc : descendants) {
                    LuaInstance* dud = (LuaInstance*)lua_newuserdata(L, sizeof(LuaInstance));
                    new (dud) LuaInstance();
                    dud->v_inst = desc;
                    luaL_getmetatable(L, "RobloxInstance");
                    lua_setmetatable(L, -2);
                    lua_rawseti(L, -2, i++);
                }
                return 1;
            }
            if (strcmp(method, "FindFirstChildOfClass") == 0) {
                const char* className = luaL_checkstring(L, 2);
                auto* child = v->FindFirstChildOfClass(className);
                if (child) {
                    LuaInstance* cud = (LuaInstance*)lua_newuserdata(L, sizeof(LuaInstance));
                    new (cud) LuaInstance();
                    cud->v_inst = child;
                    luaL_getmetatable(L, "RobloxInstance");
                    lua_setmetatable(L, -2);
                } else lua_pushnil(L);
                return 1;
            }
            if (strcmp(method, "WaitForChild") == 0) {
            const char* name = luaL_checkstring(L, 2);
            
            // Universal fix: WaitForChild should ensure cache is updated
            // We use GetChildren() to fetch fresh data from Roblox memory
            auto children = v->GetChildren();
            
            LuaVM::VirtualInstance* found = nullptr;
            for (auto* child : children) {
                if (child->Name == name) {
                    found = child;
                    break;
                }
            }
            
            if (found) {
                LuaInstance* cud = (LuaInstance*)lua_newuserdata(L, sizeof(LuaInstance));
                new (cud) LuaInstance();
                cud->v_inst = found;
                luaL_getmetatable(L, "RobloxInstance");
                lua_setmetatable(L, -2);
            } else {
                // If not found after refresh, we return nil (non-yielding implementation)
                // In a full implementation, this should yield or wait.
                lua_pushnil(L);
            }
            return 1;
        }
            if (strcmp(method, "SetAttribute") == 0) {
                const char* attrName = luaL_checkstring(L, 2);
                if (lua_isboolean(L, 3)) {
                    v->Attributes[attrName] = lua_toboolean(L, 3) != 0;
                } else if (lua_isnumber(L, 3)) {
                    v->Attributes[attrName] = lua_tonumber(L, 3);
                } else if (lua_isstring(L, 3)) {
                    v->Attributes[attrName] = std::string(lua_tostring(L, 3));
                }
                return 0;
            }
            if (strcmp(method, "GetAttribute") == 0) {
                const char* attrName = luaL_checkstring(L, 2);
                auto it = v->Attributes.find(attrName);
                if (it != v->Attributes.end()) {
                    if (std::holds_alternative<bool>(it->second)) lua_pushboolean(L, std::get<bool>(it->second));
                    else if (std::holds_alternative<double>(it->second)) lua_pushnumber(L, std::get<double>(it->second));
                    else if (std::holds_alternative<std::string>(it->second)) lua_pushstring(L, std::get<std::string>(it->second).c_str());
                    else lua_pushnil(L);
                } else lua_pushnil(L);
                return 1;
            }
            if (strcmp(method, "GetAttributes") == 0) {
                lua_newtable(L);
                for (const auto& [name, val] : v->Attributes) {
                    if (std::holds_alternative<bool>(val)) lua_pushboolean(L, std::get<bool>(val));
                    else if (std::holds_alternative<double>(val)) lua_pushnumber(L, std::get<double>(val));
                    else if (std::holds_alternative<std::string>(val)) lua_pushstring(L, std::get<std::string>(val).c_str());
                    else lua_pushnil(L);
                    lua_setfield(L, -2, name.c_str());
                }
                return 1;
            }
            if (strcmp(method, "GetService") == 0) {
                const char* serviceName = luaL_checkstring(L, 2);
                // Return stub services
                vm_globals::ensure_stub_environment();
                if (strcmp(serviceName, "Workspace") == 0) {
                    LuaInstance* sud = (LuaInstance*)lua_newuserdata(L, sizeof(LuaInstance));
                    new (sud) LuaInstance();
                    sud->v_inst = vm_globals::stub_workspace;
                    luaL_getmetatable(L, "RobloxInstance");
                    lua_setmetatable(L, -2);
                    return 1;
                }
                if (strcmp(serviceName, "Players") == 0) {
                    LuaInstance* sud = (LuaInstance*)lua_newuserdata(L, sizeof(LuaInstance));
                    new (sud) LuaInstance();
                    sud->v_inst = vm_globals::stub_players;
                    luaL_getmetatable(L, "RobloxInstance");
                    lua_setmetatable(L, -2);
                    return 1;
                }
                // Return nil for unknown services
                lua_pushnil(L);
                return 1;
            }
            if (strcmp(method, "HttpGet") == 0) {
                // Call the proper HttpGet implementation
                return L_HttpGet(L);
            }
        }
        
        // Handle real instances
        if (ud->inst.is_valid()) {
            if (strcmp(method, "GetService") == 0) {
                const char* serviceName = luaL_checkstring(L, 2);
                roblox::instance service = ud->inst.read_service(serviceName);
                if (service.is_valid()) push_roblox_instance_userdata(L, service);
                else lua_pushnil(L);
                return 1;
            }
            if (strcmp(method, "FindFirstChild") == 0) {
                const char* name = luaL_checkstring(L, 2);
                roblox::instance child = ud->inst.findfirstchild(name);
                if (child.is_valid()) push_roblox_instance_userdata(L, child);
                else lua_pushnil(L);
                return 1;
            }
            if (strcmp(method, "GetChildren") == 0) {
                std::vector<roblox::instance> children = ud->inst.get_children();
                lua_newtable(L);
                for (size_t i = 0; i < children.size(); ++i) {
                    push_roblox_instance_userdata(L, children[i]);
                    lua_rawseti(L, -2, (int)(i + 1));
                }
                return 1;
            }
            if (strcmp(method, "IsA") == 0) {
                const char* className = luaL_checkstring(L, 2);
                lua_pushboolean(L, ud->inst.get_class_name() == className);
                return 1;
            }
            if (strcmp(method, "GetMouse") == 0) {
                LuaInstance* mouse = (LuaInstance*)lua_newuserdata(L, sizeof(LuaInstance));
                new (mouse) LuaInstance();
                luaL_getmetatable(L, "Mouse");
                lua_setmetatable(L, -2);
                return 1;
            }
        }
        
        return 0;
    }

    int L_Instance_eq(lua_State* L) {
        LuaInstance* a = (LuaInstance*)lua_touserdata(L, 1);
        LuaInstance* b = (LuaInstance*)lua_touserdata(L, 2);
        
        if (!a || !b) {
            lua_pushboolean(L, false);
            return 1;
        }
        
        if (a->v_inst && b->v_inst) {
            lua_pushboolean(L, a->v_inst == b->v_inst);
        } else if (a->inst.is_valid() && b->inst.is_valid()) {
            lua_pushboolean(L, a->inst.address == b->inst.address);
        } else {
            lua_pushboolean(L, false);
        }
        return 1;
    }

    int L_Instance_FindFirstChild(lua_State* L) {
        return L_Instance_namecall(L);
    }

    int L_Instance_WaitForChild(lua_State* L) {
        // For VM, WaitForChild acts like FindFirstChild
        return L_Instance_namecall(L);
    }

    int L_Mouse_index(lua_State* L) {
        const char* key = lua_tostring(L, 2);
        if (!key) return 0;
        
        POINT pt;
        GetCursorPos(&pt);
        
        if (strcmp(key, "X") == 0) { lua_pushnumber(L, pt.x); return 1; }
        if (strcmp(key, "Y") == 0) { lua_pushnumber(L, pt.y); return 1; }
        
        return 0;
    }
}
