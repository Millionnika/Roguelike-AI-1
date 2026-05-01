internal sealed class SpaceCombatLocalizationService : ILocalizationService
{
    public string Localize(string key, bool ru)
    {
        switch (key)
        {
            case "overview": return ru ? "ОБЗОР" : "OVERVIEW";
            case "enemy_header": return ru ? "ID          ТИП        ДИСТ.      СТАТУС" : "ID          TYPE        DIST       STATUS";
            case "combat_log": return ru ? "ЖУРНАЛ БОЯ" : "COMBAT LOG";
            case "ship_status": return ru ? "СОСТОЯНИЕ КОРАБЛЯ" : "SHIP STATUS";
            case "target_none": return ru ? "Цель: нет" : "Target: none";
            case "target_none_name": return ru ? "нет" : "none";
            case "ship_none": return ru ? "Корабль: нет" : "Ship: none";
            case "capacitor": return ru ? "Энергия: " : "Capacitor: ";
            case "ship_label": return ru ? "Корабль: " : "Ship: ";
            case "target_label": return ru ? "Цель: " : "Target: ";
            case "level_label": return ru ? "Уровень: " : "Level: ";
            case "xp_label": return ru ? "XP: " : "XP: ";
            case "stat_speed": return ru ? "Скорость" : "Speed";
            case "stat_shield": return ru ? "Щит" : "Shield";
            case "stat_armor": return ru ? "Броня" : "Armor";
            case "stat_hull": return ru ? "Корпус" : "Hull";
            case "stat_capacitor": return ru ? "Энергия" : "Capacitor";
            case "stat_recharge": return ru ? "Перезаряд" : "Recharge";
            case "stat_weapon_slots": return ru ? "Слоты оружия" : "Weapon slots";
            case "stat_module_slots": return ru ? "Слоты модулей" : "Module slots";
            case "stat_guns": return ru ? "Пушки" : "Guns";
            case "distance": return ru ? "Дистанция: " : "Distance: ";
            case "timeline_missing": return ru ? "Таймлайн волн не назначен." : "Wave timeline is not assigned.";
            case "timeline_complete": return ru ? "Таймлайн завершён." : "Timeline complete.";
            case "timeline_next_event": return ru ? "Следующий эвент через " : "Next event in ";
            case "seconds_short": return ru ? "с" : "s";
            case "warp_inactive": return ru ? "Варп-гейт неактивен" : "Warp gate inactive";
            case "warp_active": return ru ? "Варп-гейт активен. Подлетите и нажмите [G]." : "Warp gate active. Move to the ring and press [G].";
            case "status_menu": return ru ? "Меню: 1-3 выбор корабля  Enter запуск" : "Menu: 1-3 choose ship  Enter start";
            case "status_gameover": return ru ? "ИГРА ОКОНЧЕНА" : "GAME OVER";
            case "status_levelup": return ru ? "НОВЫЙ УРОВЕНЬ: выберите 1-3" : "LEVEL UP: choose 1-3";
            case "status_play_desktop": return ru ? "WASD или ЛКМ движение  ЛКМ по списку выбирает цель  1-4 модули  G варп" : "WASD or LMB move  LMB on list selects target  1-4 modules  G warp";
            case "status_play_mobile": return ru ? "Джойстик слева  тап по космосу автополёт  тап по списку выбирает цель" : "Left joystick  tap space to autopilot  tap list to target";
            case "main_title": return ru ? "КОСМИЧЕСКИЙ РУБЕЖ" : "SPACE FRONTIER";
            case "main_subtitle": return ru ? "Тактический бой среди звёзд" : "Tactical combat among the stars";
            case "menu_new_game": return ru ? "НОВАЯ ИГРА" : "NEW GAME";
            case "menu_continue": return ru ? "ПРОДОЛЖИТЬ" : "CONTINUE";
            case "menu_settings": return ru ? "НАСТРОЙКИ" : "SETTINGS";
            case "menu_exit": return ru ? "ВЫХОД" : "EXIT";
            case "menu_short": return ru ? "МЕНЮ" : "MENU";
            case "retry": return ru ? "ПОВТОРИТЬ" : "RETRY";
            case "confirm_title": return ru ? "ПОДТВЕРЖДЕНИЕ" : "CONFIRM";
            case "confirm_yes": return ru ? "ДА" : "YES";
            case "confirm_no": return ru ? "НЕТ" : "NO";
            case "confirm_exit": return ru ? "Вы уверены, что хотите выйти?" : "Are you sure you want to exit?";
            case "confirm_to_menu": return ru ? "Выйти в главное меню?" : "Return to main menu?";
            case "pause_to_menu": return ru ? "В МЕНЮ" : "MAIN MENU";
            case "hangar_title": return ru ? "АНГАР ФЛОТА" : "STAR HANGAR";
            case "hangar_subtitle": return ru ? "Выберите корабль и начните вылет" : "Choose your ship and launch into the sector";
            case "hangar_hint_desktop": return ru ? "1-3 выбор корпуса, Enter или START для вылета." : "Press 1-3 to select a hull, Enter or START to launch.";
            case "hangar_hint_mobile": return ru ? "Тап по карточке корабля и кнопке START для вылета." : "Tap a ship card and START to launch.";
            case "start_operation": return ru ? "НАЧАТЬ" : "START";
            case "back": return ru ? "НАЗАД" : "BACK";
            case "settings_title": return ru ? "НАСТРОЙКИ" : "SETTINGS";
            case "settings_subtitle": return ru ? "Язык интерфейса и лимит кадров" : "Interface language and frame rate limit";
            case "settings_language": return ru ? "Язык" : "Language";
            case "settings_fps": return ru ? "FPS" : "FPS";
            case "lang_ru": return "RU";
            case "lang_eng": return "ENG";
            case "joystick_hint": return ru ? "Джойстик" : "Joystick";
            case "perk_title": return ru ? "НОВЫЙ УРОВЕНЬ" : "LEVEL UP";
            case "perk_pick": return ru ? "Нажмите 1, 2 или 3 для выбора." : "Press 1, 2 or 3 to choose.";
            case "log_docked": return ru ? "Корабль в ангаре. Системы в режиме ожидания." : "Docked and awaiting launch.";
            case "log_choose_hull": return ru ? "Выберите корпус и начните операцию." : "Choose a hull and begin operation.";
            case "log_launch": return ru ? "Старт подтверждён: " : "Launch confirmed: ";
            case "log_sector_scan": return ru ? "Сканирование сектора завершено. Обнаружены цели." : "Sector scan complete. Hostiles incoming.";
            case "log_hostiles": return ru ? "Обнаружено противников: " : "Hostiles detected: ";
            case "log_target_locked": return ru ? "Цель захвачена: " : "Target locked: ";
            case "log_move_gate": return ru ? "Подлетите ближе к варп-гейту для прыжка." : "Move closer to the warp gate to jump.";
            case "log_cap_dry": return ru ? "Энергия исчерпана. " : "Capacitor dry. ";
            case "log_offline": return ru ? " отключён." : " offline.";
            case "log_cap_insufficient": return ru ? "Недостаточно энергии для " : "Insufficient capacitor for ";
            case "log_shot_missed": return ru ? "Промах по " : "Shot missed ";
            case "log_hit": return ru ? "Попадание по " : "Hit ";
            case "log_for": return ru ? " на " : " for ";
            case "log_destroyed": return ru ? " уничтожен" : " destroyed";
            case "log_levelup": return ru ? "Достигнут новый уровень. Выберите улучшение." : "Level up reached. Choose an upgrade.";
            case "log_perk_selected": return ru ? "Выбрано улучшение: " : "Perk selected: ";
            case "log_warp_active": return ru ? "Варп-гейт активирован" : "Warp gate activated";
            case "log_warp_sector": return ru ? "Прыжок выполнен. Вход в сектор " : "Warp jump complete. Entering sector ";
            case "log_enemy_hits": return ru ? " наносит урон " : " hits you for ";
            case "log_hull_breach": return ru ? "Корабль уничтожен. Корпус разрушен." : "Hull breach detected. Ship destroyed.";
            case "log_module_on": return ru ? " Модуль активирован." : " engaged.";
            case "log_module_off": return ru ? " Модуль отключён." : " disengaged.";
            case "perk_damage": return ru ? "Урон +15%" : "Damage +15%";
            case "perk_capacitor": return ru ? "Энергия +20%" : "Capacitor +20%";
            case "perk_shield": return ru ? "Щит +25%" : "Shield +25%";
            case "perk_speed": return ru ? "Скорость +20%" : "Speed +20%";
            case "perk_repair": return ru ? "Ремонт +30%" : "Repair +30%";
            default: return key;
        }
    }

    public string GetShipRoleText(ShipDataSO ship, bool ru)
    {
        if (!ru)
        {
            return ship != null ? ship.role : string.Empty;
        }

        if (ship == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(ship.roleRu))
        {
            return ship.roleRu;
        }

        switch (ship.displayName)
        {
            case "Aegis": return "Сбалансированный фрегат";
            case "Bulwark": return "Тяжёлый крейсер";
            case "Raptor": return "Ударный перехватчик";
            default: return ship.role;
        }
    }

    public string GetShipDescriptionText(ShipDataSO ship, bool ru)
    {
        if (!ru)
        {
            return ship != null ? ship.description : string.Empty;
        }

        if (ship == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(ship.descriptionRu))
        {
            return ship.descriptionRu;
        }

        switch (ship.displayName)
        {
            case "Aegis":
                return "Универсальный корабль с надёжной энергосистемой и хорошей живучестью. Лучший выбор для спокойного старта.";
            case "Bulwark":
                return "Медленный, но очень крепкий корабль с мощными щитами и бронёй. Дольше всех держится в затяжном бою.";
            case "Raptor":
                return "Быстрый охотник с повышенным уроном и бодрым восстановлением энергии. Требует движения и точного приоритета целей.";
            default:
                return ship.description;
        }
    }
}
