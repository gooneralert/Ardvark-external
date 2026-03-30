// ============================================================================
// LuaVM Drawing - Drawing API implementation
// ============================================================================

#include "luavm_common.h"

namespace util {
    // Drawing render implementations
    void LuaVM::DrawingLine::render(ImDrawList* dl) {
        if (!visible) return;
        ImU32 col = IM_COL32(color.Value.x * 255, color.Value.y * 255, color.Value.z * 255, transparency * 255);
        dl->AddLine(from, to, col, thickness);
    }

    void LuaVM::DrawingText::render(ImDrawList* dl) {
        if (!visible || text.empty()) return;
        ImU32 col = IM_COL32(color.Value.x * 255, color.Value.y * 255, color.Value.z * 255, transparency * 255);
        
        ImVec2 pos = position;
        if (center) {
            ImVec2 textSize = ImGui::CalcTextSize(text.c_str());
            pos.x -= textSize.x * 0.5f;
        }
        
        if (outline) {
            ImU32 outCol = IM_COL32(outline_color.Value.x * 255, outline_color.Value.y * 255, outline_color.Value.z * 255, transparency * 255);
            dl->AddText(pos + ImVec2(-1, 0), outCol, text.c_str());
            dl->AddText(pos + ImVec2(1, 0), outCol, text.c_str());
            dl->AddText(pos + ImVec2(0, -1), outCol, text.c_str());
            dl->AddText(pos + ImVec2(0, 1), outCol, text.c_str());
        }
        dl->AddText(pos, col, text.c_str());
    }

    void LuaVM::DrawingSquare::render(ImDrawList* dl) {
        if (!visible) return;
        ImU32 col = IM_COL32(color.Value.x * 255, color.Value.y * 255, color.Value.z * 255, transparency * 255);
        if (filled) dl->AddRectFilled(position, position + size, col);
        else dl->AddRect(position, position + size, col, 0, 0, thickness);
    }

    void LuaVM::DrawingCircle::render(ImDrawList* dl) {
        if (!visible) return;
        ImU32 col = IM_COL32(color.Value.x * 255, color.Value.y * 255, color.Value.z * 255, transparency * 255);
        int segments = num_sides > 0 ? num_sides : 36;
        if (filled) dl->AddCircleFilled(position, radius, col, segments);
        else dl->AddCircle(position, radius, col, segments, thickness);
    }

    void LuaVM::DrawingTriangle::render(ImDrawList* dl) {
        if (!visible) return;
        ImU32 col = IM_COL32(color.Value.x * 255, color.Value.y * 255, color.Value.z * 255, transparency * 255);
        if (filled) dl->AddTriangleFilled(p1, p2, p3, col);
        else dl->AddTriangle(p1, p2, p3, col, thickness);
    }

    // Lua Drawing wrapper
    struct LuaDrawing {
        LuaVM::DrawingBase* base;
    };

    int L_Drawing_new(lua_State* L) {
        const char* type = luaL_checkstring(L, 1);
        
        LuaVM::DrawingBase* drawing = nullptr;
        
        if (strcmp(type, "Line") == 0) {
            drawing = new LuaVM::DrawingLine();
        } else if (strcmp(type, "Text") == 0) {
            drawing = new LuaVM::DrawingText();
        } else if (strcmp(type, "Square") == 0) {
            drawing = new LuaVM::DrawingSquare();
        } else if (strcmp(type, "Circle") == 0) {
            drawing = new LuaVM::DrawingCircle();
        } else if (strcmp(type, "Triangle") == 0) {
            drawing = new LuaVM::DrawingTriangle();
        } else {
            luaL_error(L, "Invalid drawing type: %s", type);
            return 0;
        }
        
        LuaVM::get().add_drawing(drawing);
        
        LuaDrawing* ud = (LuaDrawing*)lua_newuserdata(L, sizeof(LuaDrawing));
        ud->base = drawing;
        luaL_getmetatable(L, "DrawingInstance");
        lua_setmetatable(L, -2);
        
        return 1;
    }

    int L_Drawing_index(lua_State* L) {
        LuaDrawing* ud = (LuaDrawing*)lua_touserdata(L, 1);
        if (!ud || !ud->base) return 0;
        
        const char* key = lua_tostring(L, 2);
        if (!key) return 0;
        
        LuaVM::DrawingBase* base = ud->base;
        
        // Common properties
        if (strcmp(key, "Visible") == 0) { lua_pushboolean(L, base->visible); return 1; }
        if (strcmp(key, "ZIndex") == 0) { lua_pushinteger(L, base->zindex); return 1; }
        if (strcmp(key, "Transparency") == 0) { lua_pushnumber(L, base->transparency); return 1; }
        if (strcmp(key, "Color") == 0) { 
            push_color3_userdata(L, base->color.Value.x, base->color.Value.y, base->color.Value.z);
            return 1;
        }
        if (strcmp(key, "Remove") == 0) {
            lua_pushcfunction(L, [](lua_State* L) -> int {
                LuaDrawing* ud = (LuaDrawing*)lua_touserdata(L, 1);
                if (ud && ud->base) {
                    LuaVM::get().remove_drawing(ud->base);
                    ud->base = nullptr;
                }
                return 0;
            }, "Remove");
            return 1;
        }
        
        // Type-specific properties
        if (auto* line = dynamic_cast<LuaVM::DrawingLine*>(base)) {
            if (strcmp(key, "From") == 0) { push_vector2_userdata(L, line->from.x, line->from.y); return 1; }
            if (strcmp(key, "To") == 0) { push_vector2_userdata(L, line->to.x, line->to.y); return 1; }
            if (strcmp(key, "Thickness") == 0) { lua_pushnumber(L, line->thickness); return 1; }
        }
        else if (auto* text = dynamic_cast<LuaVM::DrawingText*>(base)) {
            if (strcmp(key, "Text") == 0) { lua_pushstring(L, text->text.c_str()); return 1; }
            if (strcmp(key, "Position") == 0) { push_vector2_userdata(L, text->position.x, text->position.y); return 1; }
            if (strcmp(key, "Size") == 0) { lua_pushnumber(L, text->size); return 1; }
            if (strcmp(key, "Center") == 0) { lua_pushboolean(L, text->center); return 1; }
            if (strcmp(key, "Outline") == 0) { lua_pushboolean(L, text->outline); return 1; }
        }
        else if (auto* square = dynamic_cast<LuaVM::DrawingSquare*>(base)) {
            if (strcmp(key, "Position") == 0) { push_vector2_userdata(L, square->position.x, square->position.y); return 1; }
            if (strcmp(key, "Size") == 0) { push_vector2_userdata(L, square->size.x, square->size.y); return 1; }
            if (strcmp(key, "Filled") == 0) { lua_pushboolean(L, square->filled); return 1; }
            if (strcmp(key, "Thickness") == 0) { lua_pushnumber(L, square->thickness); return 1; }
        }
        else if (auto* circle = dynamic_cast<LuaVM::DrawingCircle*>(base)) {
            if (strcmp(key, "Position") == 0) { push_vector2_userdata(L, circle->position.x, circle->position.y); return 1; }
            if (strcmp(key, "Radius") == 0) { lua_pushnumber(L, circle->radius); return 1; }
            if (strcmp(key, "Filled") == 0) { lua_pushboolean(L, circle->filled); return 1; }
            if (strcmp(key, "NumSides") == 0) { lua_pushinteger(L, circle->num_sides); return 1; }
            if (strcmp(key, "Thickness") == 0) { lua_pushnumber(L, circle->thickness); return 1; }
        }
        else if (auto* tri = dynamic_cast<LuaVM::DrawingTriangle*>(base)) {
            if (strcmp(key, "PointA") == 0) { push_vector2_userdata(L, tri->p1.x, tri->p1.y); return 1; }
            if (strcmp(key, "PointB") == 0) { push_vector2_userdata(L, tri->p2.x, tri->p2.y); return 1; }
            if (strcmp(key, "PointC") == 0) { push_vector2_userdata(L, tri->p3.x, tri->p3.y); return 1; }
            if (strcmp(key, "Filled") == 0) { lua_pushboolean(L, tri->filled); return 1; }
            if (strcmp(key, "Thickness") == 0) { lua_pushnumber(L, tri->thickness); return 1; }
        }
        
        return 0;
    }

    int L_Drawing_newindex(lua_State* L) {
        LuaDrawing* ud = (LuaDrawing*)lua_touserdata(L, 1);
        if (!ud || !ud->base) return 0;
        
        const char* key = lua_tostring(L, 2);
        if (!key) return 0;
        
        LuaVM::DrawingBase* base = ud->base;
        
        // Common properties
        if (strcmp(key, "Visible") == 0) { base->visible = lua_toboolean(L, 3); return 0; }
        if (strcmp(key, "ZIndex") == 0) { base->zindex = (int)lua_tointeger(L, 3); return 0; }
        if (strcmp(key, "Transparency") == 0) { base->transparency = (float)lua_tonumber(L, 3); return 0; }
        if (strcmp(key, "Color") == 0) {
            LuaColor3* c = try_get_color3_userdata(L, 3);
            if (c) base->color = ImColor(c->r, c->g, c->b);
            return 0;
        }
        
        // Type-specific properties
        if (auto* line = dynamic_cast<LuaVM::DrawingLine*>(base)) {
            if (strcmp(key, "From") == 0) {
                Vector2* v = try_get_vector2_userdata(L, 3);
                if (v) line->from = ImVec2(v->x, v->y);
                return 0;
            }
            if (strcmp(key, "To") == 0) {
                Vector2* v = try_get_vector2_userdata(L, 3);
                if (v) line->to = ImVec2(v->x, v->y);
                return 0;
            }
            if (strcmp(key, "Thickness") == 0) { line->thickness = (float)lua_tonumber(L, 3); return 0; }
        }
        else if (auto* text = dynamic_cast<LuaVM::DrawingText*>(base)) {
            if (strcmp(key, "Text") == 0) { text->text = lua_tostring(L, 3); return 0; }
            if (strcmp(key, "Position") == 0) {
                Vector2* v = try_get_vector2_userdata(L, 3);
                if (v) text->position = ImVec2(v->x, v->y);
                return 0;
            }
            if (strcmp(key, "Size") == 0) { text->size = (float)lua_tonumber(L, 3); return 0; }
            if (strcmp(key, "Center") == 0) { text->center = lua_toboolean(L, 3); return 0; }
            if (strcmp(key, "Outline") == 0) { text->outline = lua_toboolean(L, 3); return 0; }
        }
        else if (auto* square = dynamic_cast<LuaVM::DrawingSquare*>(base)) {
            if (strcmp(key, "Position") == 0) {
                Vector2* v = try_get_vector2_userdata(L, 3);
                if (v) square->position = ImVec2(v->x, v->y);
                return 0;
            }
            if (strcmp(key, "Size") == 0) {
                Vector2* v = try_get_vector2_userdata(L, 3);
                if (v) square->size = ImVec2(v->x, v->y);
                return 0;
            }
            if (strcmp(key, "Filled") == 0) { square->filled = lua_toboolean(L, 3); return 0; }
            if (strcmp(key, "Thickness") == 0) { square->thickness = (float)lua_tonumber(L, 3); return 0; }
        }
        else if (auto* circle = dynamic_cast<LuaVM::DrawingCircle*>(base)) {
            if (strcmp(key, "Position") == 0) {
                Vector2* v = try_get_vector2_userdata(L, 3);
                if (v) circle->position = ImVec2(v->x, v->y);
                return 0;
            }
            if (strcmp(key, "Radius") == 0) { circle->radius = (float)lua_tonumber(L, 3); return 0; }
            if (strcmp(key, "Filled") == 0) { circle->filled = lua_toboolean(L, 3); return 0; }
            if (strcmp(key, "NumSides") == 0) { circle->num_sides = (int)lua_tointeger(L, 3); return 0; }
            if (strcmp(key, "Thickness") == 0) { circle->thickness = (float)lua_tonumber(L, 3); return 0; }
        }
        else if (auto* tri = dynamic_cast<LuaVM::DrawingTriangle*>(base)) {
            if (strcmp(key, "PointA") == 0) {
                Vector2* v = try_get_vector2_userdata(L, 3);
                if (v) tri->p1 = ImVec2(v->x, v->y);
                return 0;
            }
            if (strcmp(key, "PointB") == 0) {
                Vector2* v = try_get_vector2_userdata(L, 3);
                if (v) tri->p2 = ImVec2(v->x, v->y);
                return 0;
            }
            if (strcmp(key, "PointC") == 0) {
                Vector2* v = try_get_vector2_userdata(L, 3);
                if (v) tri->p3 = ImVec2(v->x, v->y);
                return 0;
            }
            if (strcmp(key, "Filled") == 0) { tri->filled = lua_toboolean(L, 3); return 0; }
            if (strcmp(key, "Thickness") == 0) { tri->thickness = (float)lua_tonumber(L, 3); return 0; }
        }
        
        return 0;
    }

    int L_Drawing_gc(lua_State* L) {
        LuaDrawing* ud = (LuaDrawing*)lua_touserdata(L, 1);
        if (ud && ud->base) {
            LuaVM::get().remove_drawing(ud->base);
            ud->base = nullptr;
        }
        return 0;
    }

    int L_Drawing_namecall(lua_State* L) {
        LuaDrawing* ud = (LuaDrawing*)lua_touserdata(L, 1);
        if (!ud || !ud->base) return 0;
        
        const char* method = lua_namecallatom(L, nullptr);
        if (!method) return 0;
        
        if (strcmp(method, "Remove") == 0) {
            LuaVM::get().remove_drawing(ud->base);
            ud->base = nullptr;
            return 0;
        }
        
        return 0;
    }
}
