-- DwarfVolon: lê a tela "Preparar com cuidado" (viewscreen_setupdwarfgamest) e imprime JSON.
-- Somente leitura. Pode ser testado no console do DFHack com: lua -f embark.lua
--
-- Saída (uma linha):
--   { "ok": true, "status": "embark", ... }        na tela de embarque
--   { "ok": true, "status": "other", "screen": "..." }  em outra tela
--   { "ok": false, "error": "..." }                se algo falhar
--
-- Só números inteiros: o encoder de JSON do DFHack segue o locale do jogo
-- e pode escrever decimais com vírgula.

local json = require('json')

local function utf(s)
    return dfhack.df2utf(s or '')
end

local function name_of(name)
    local ok, native = pcall(dfhack.translation.translateName, name, false)
    local ok2, english = pcall(dfhack.translation.translateName, name, true)
    return utf(ok and native or ''), utf(ok2 and english or '')
end

local function enum_list(enum_type, values)
    local out = {}
    for _, v in ipairs(values) do table.insert(out, enum_type[v] or tostring(v)) end
    return out
end

-- Nome (em inglês, como o jogo mostra) do objeto de um gosto. nil se não der para resolver.
local function material_name(mattype, matindex)
    local info = dfhack.matinfo.decode(mattype, matindex)
    return info and info:toString() or nil
end

local function form_name(kind, id)
    local form = df[kind] and df[kind].find(id)
    return form and dfhack.translation.translateName(form.name, true) or nil
end

local pref_resolvers = {
    LikeMaterial = function(p) return material_name(p.mattype, p.matindex) end,
    LikeFood = function(p) return material_name(p.mattype, p.matindex) end,
    LikeCreature = function(p) local c = df.creature_raw.find(p.creature_id) return c and c.name[1] end,
    HateCreature = function(p) local c = df.creature_raw.find(p.creature_id) return c and c.name[1] end,
    LikeItem = function(p)
        if p.item_subtype >= 0 then
            local def = dfhack.items.getSubtypeDef(p.item_type, p.item_subtype)
            if def then return def.name_plural or def.name end
        end
        local id = df.item_type[p.item_type]
        return id and id:lower():gsub('_', ' ') or nil
    end,
    LikePlant = function(p) local pl = df.plant_raw.find(p.plant_id) return pl and pl.name_plural end,
    LikeTree = function(p) local pl = df.plant_raw.find(p.plant_id) return pl and pl.name_plural end,
    LikeColor = function(p) local c = df.global.world.raws.descriptors.colors[p.color_id] return c and c.name end,
    LikeShape = function(p) local sh = df.global.world.raws.descriptors.shapes[p.shape_id] return sh and sh.name_plural end,
    LikePoeticForm = function(p) return form_name('poetic_form', p.poetic_form_id) end,
    LikeMusicalForm = function(p) return form_name('musical_form', p.musical_form_id) end,
    LikeDanceForm = function(p) return form_name('dance_form', p.dance_form_id) end,
}

local function collect_preferences(soul)
    local out = {}
    for _, p in ipairs(soul.preferences) do
        local kind = df.unitpref_type[p.type] or tostring(p.type)
        local resolver = pref_resolvers[kind]
        local ok, text = pcall(function() return resolver and resolver(p) end)
        table.insert(out, { kind = kind, text = utf(ok and text or '') })
    end
    return out
end

local function collect_dwarf(index, info, unit)
    -- No embarque de fortaleza, nome, idade, alma e habilidades de fundo ficam na unidade
    -- (s_unit); dwarf_info só tem o que é escolhido na tela (skilllevel, skill_picks_left).
    local name = (unit and unit.name.has_name) and unit.name or info.name
    local native, english = name_of(name)
    local d = {
        index = index,
        name = native,
        name_english = english,
        nickname = utf(name.nickname),
        picks_left = info.skill_picks_left,
        birth_year = unit and unit.birth_year or info.birth_year,
        age = unit and math.floor(dfhack.units.getAge(unit)) or nil,
        skills = {},
        background_skills = {},
        preferences = {},
        beliefs = {},
        dreams = {},
        physical = {},
        mental = {},
        traits = {},
    }

    -- pairs() num array indexado por enum devolve o nome (ex.: "MINING") como chave.
    for skill, level in pairs(info.skilllevel) do
        if level > 0 then
            local id = type(skill) == 'string' and skill or df.job_skill[skill]
            table.insert(d.skills, { id = id, level = level })
        end
    end
    table.sort(d.skills, function(a, b) return a.level > b.level or (a.level == b.level and a.id < b.id) end)

    if unit then
        d.unit_id = unit.id
        d.hist_figure_id = unit.hist_figure_id
        d.sex = unit.sex
        d.profession = df.profession[unit.profession] or ''
        for attr = 0, df.physical_attribute_type._last_item do
            d.physical[df.physical_attribute_type[attr]] =
                math.floor(dfhack.units.getPhysicalAttrValue(unit, attr))
        end
        for attr = 0, df.mental_attribute_type._last_item do
            d.mental[df.mental_attribute_type[attr]] =
                math.floor(dfhack.units.getMentalAttrValue(unit, attr))
        end
    end

    -- Personalidade: valores 0–100 por faceta (50 = neutro).
    local soul = unit and unit.status.current_soul

    -- Habilidades que o anão já traz (ex.: música, poesia); não saem dos pontos de habilidade.
    if soul then
        for _, sk in ipairs(soul.skills) do
            if sk.rating > 0 then
                table.insert(d.background_skills, { id = df.job_skill[sk.id], level = sk.rating })
            end
        end
        table.sort(d.background_skills, function(a, b) return a.level > b.level or (a.level == b.level and a.id < b.id) end)
    end

    local pers = soul and soul.personality or info.pers
    if pers then
        for facet = 0, df.personality_facet_type._last_item do
            d.traits[df.personality_facet_type[facet]] = pers.traits[facet]
        end
    end

    if soul then
        d.preferences = collect_preferences(soul)
        -- Crenças pessoais (as que diferem da cultura): força de -50 a 50.
        for _, v in ipairs(soul.personality.values) do
            table.insert(d.beliefs, { id = df.value_type[v.type] or tostring(v.type), strength = v.strength })
        end
        for _, dream in ipairs(soul.personality.dreams) do
            table.insert(d.dreams, df.goal_type[dream.type] or tostring(dream.type))
        end
    end

    return d
end

local function collect(scr)
    if not df.viewscreen_setupdwarfgamest:is_instance(scr) then
        local focus = scr and table.concat(dfhack.gui.getFocusStrings(scr), ',') or ''
        return { ok = true, status = 'other', screen = focus }
    end

    local result = {
        ok = true,
        status = 'embark',
        focus = table.concat(dfhack.gui.getFocusStrings(scr), ','),
        points_remaining = scr.points_remaining,
        embark_points = df.global.world.worldgen.worldgen_parms.embark_points,
        cur_year = df.global.cur_year,
        -- Pasta do mundo (ex.: region2): chave para guardar as decisões do embarque.
        save_dir = df.global.world.cur_savegame.save_dir,
        world = utf(dfhack.translation.translateName(df.global.world.world_data.name, true)),
        skill_tabs = {},
        dwarves = {},
    }

    for tab = 0, df.embark_skill_tab_type._last_item do
        local skills = {}
        for _, skill in ipairs(scr.embark_skills[tab]) do table.insert(skills, skill) end
        result.skill_tabs[df.embark_skill_tab_type[tab]] = enum_list(df.job_skill, skills)
    end

    for i, info in ipairs(scr.dwarf_info) do
        local unit = scr.s_unit[i]
        table.insert(result.dwarves, collect_dwarf(i, info, unit))
    end

    -- Faixas de atributos da casta (7 pontos: mínimo ... mediana (4º) ... máximo),
    -- usadas para dizer se um valor é alto ou baixo para um anão.
    local first = scr.dwarf_info[0]
    if first then
        local creature = df.creature_raw.find(first.race)
        local caste = creature and creature.caste[math.max(first.caste, 0)]
        if caste then
            result.ranges = { physical = {}, mental = {} }
            for attr = 0, df.physical_attribute_type._last_item do
                local r = {}
                for k = 0, 6 do r[k + 1] = caste.attributes.phys_att_range[attr][k] end
                result.ranges.physical[df.physical_attribute_type[attr]] = r
            end
            for attr = 0, df.mental_attribute_type._last_item do
                local r = {}
                for k = 0, 6 do r[k + 1] = caste.attributes.ment_att_range[attr][k] end
                result.ranges.mental[df.mental_attribute_type[attr]] = r
            end
        end
    end

    -- Ficha de unidade aberta no jogo: o DF só gera o texto de descrição com ela aberta.
    -- O app decide pelo sub_tab se o texto é da aba de personalidade e guarda por anão.
    local vs = df.global.game.main_interface.view_sheets
    if vs.open and #vs.personality_raw_str > 0 then
        for _, unit in ipairs(scr.s_unit) do
            if unit.id == vs.active_id then
                local blocks = {}
                for _, str in ipairs(vs.personality_raw_str) do table.insert(blocks, utf(str.value)) end
                result.sheet = {
                    unit_id = vs.active_id,
                    sub_tab = vs.active_sub_tab,
                    personality_tab = vs.personality_active_tab,
                    blocks = blocks,
                }
            end
        end
    end

    -- Nomes (em inglês) de todas as habilidades, para as que não tiverem tradução no app.
    result.skill_captions = {}
    for skill = 0, df.job_skill._last_item do
        local id = df.job_skill[skill]
        if id then result.skill_captions[id] = df.job_skill.attrs[skill].caption end
    end

    return result
end

local ok, result = pcall(function()
    -- DWARFVOLON_SCREEN permite testar com uma tela montada à mão.
    return collect(DWARFVOLON_SCREEN or dfhack.gui.getDFViewscreen(true))
end)
if not ok then result = { ok = false, error = tostring(result) } end
print(json.encode(result, { pretty = false }))
