-- DwarfVolon: aplica um plano de pontos de habilidade na tela "Preparar com cuidado".
-- O app coloca antes deste código: local PLAN_JSON = [==[ {...} ]==]
--   { "dwarves": [ { "index": 0, "unit_id": 11295, "skills": { "MINING": 1, "SET_BONE": 5 } }, ... ] }
-- Para cada anão do plano, as habilidades escolhidas passam a ser exatamente as da lista
-- (as que o anão "já sabe" não mudam) e os pontos livres são recalculados.
-- Regras (as mesmas do app): 1 ponto por nível, nível 1 a 5, limite = pontos livres + níveis já escolhidos.
-- Tudo é validado antes de mudar qualquer coisa: ou aplica tudo, ou nada.
--
-- Saída (uma linha de JSON): { "ok": true, "applied": [ { "index": 0, "picks_left": 0 } ] }
--                         ou { "ok": false, "error": "..." }

local json = require('json')
local MAX_LEVEL = 5

local function run()
    local scr = dfhack.gui.getDFViewscreen(true)
    if not df.viewscreen_setupdwarfgamest:is_instance(scr) then
        error('o jogo não está na tela Preparar com cuidado')
    end

    local plan = json.decode(PLAN_JSON)
    local checked = {}

    for _, p in ipairs(plan.dwarves or {}) do
        local index = math.floor(p.index)
        local info = scr.dwarf_info[index]
        local unit = scr.s_unit[index]
        if not info then error(('anão %d não existe na tela'):format(index)) end
        if type(p.unit_id) == 'number' and unit and unit.id ~= math.floor(p.unit_id) then
            error('a lista de anões mudou no jogo; atualize o DwarfVolon e tente de novo')
        end

        local spent = 0
        for _, level in pairs(info.skilllevel) do spent = spent + level end
        local budget = info.skill_picks_left + spent

        local cost = 0
        for skill, level in pairs(p.skills or {}) do
            if df.job_skill[skill] == nil then error('habilidade desconhecida: ' .. tostring(skill)) end
            if type(level) ~= 'number' or level < 1 or level > MAX_LEVEL then
                error(('nível inválido para %s: %s'):format(skill, tostring(level)))
            end
            cost = cost + math.floor(level)
        end
        if cost > budget then
            error(('o plano do anão %d gasta %d pontos, mas o limite é %d'):format(index, cost, budget))
        end
        table.insert(checked, { index = index, info = info, plan = p, picks_left = budget - cost })
    end

    local applied = {}
    for _, c in ipairs(checked) do
        for skill, _ in pairs(c.info.skilllevel) do c.info.skilllevel[skill] = 0 end
        for skill, level in pairs(c.plan.skills or {}) do c.info.skilllevel[skill] = math.floor(level) end
        c.info.skill_picks_left = c.picks_left
        table.insert(applied, { index = c.index, picks_left = c.picks_left })
    end
    return { ok = true, applied = applied }
end

local ok, result = pcall(run)
if not ok then result = { ok = false, error = tostring(result) } end
print(json.encode(result, { pretty = false }))
