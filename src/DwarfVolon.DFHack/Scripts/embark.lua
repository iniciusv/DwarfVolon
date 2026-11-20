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

local function collect_dwarf(index, info, unit)
    local native, english = name_of(info.name)
    local d = {
        index = index,
        name = native,
        name_english = english,
        picks_left = info.skill_picks_left,
        birth_year = info.birth_year,
        skills = {},
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
    local pers = soul and soul.personality or info.pers
    if pers then
        for facet = 0, df.personality_facet_type._last_item do
            d.traits[df.personality_facet_type[facet]] = pers.traits[facet]
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
