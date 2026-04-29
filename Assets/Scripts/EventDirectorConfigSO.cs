using UnityEngine;

public enum Tension
{
    Low,
    Normal,
    High
}

[CreateAssetMenu(menuName = "Roguelike/Event Director Config", fileName = "EventDirectorConfig")]
public sealed class EventDirectorConfigSO : ScriptableObject
{
    [Header("Порог состояния игрока")]
    [Tooltip("Если доля корпуса игрока после локации меньше или равна этому значению, директор считает ситуацию напряженной и чаще предлагает восстановление.")]
    [Range(0f, 1f)] public float lowHullThreshold = 0.35f;
    [Tooltip("Если доля потерянного корпуса за последнюю локацию больше или равна этому значению, директор считает бой тяжелым и повышает шанс безопасных локаций.")]
    [Range(0f, 1f)] public float heavyDamageThreshold = 0.35f;
    [Tooltip("Сколько боевых локаций подряд можно пройти до штрафа к новым обычным и элитным боям.")]
    [Min(1)] public int combatStreakLimit = 3;

    [Header("Высокое напряжение")]
    [Tooltip("Множитель веса ремонта, когда игрок в плохом состоянии или получил много урона.")]
    [Min(0f)] public float repairWeightBoostWhenLowHull = 2.0f;
    [Tooltip("Множитель веса магазина, когда игрок в плохом состоянии или получил много урона.")]
    [Min(0f)] public float shopWeightBoostWhenLowHull = 1.5f;
    [Tooltip("Множитель веса ресурсной локации, когда игрок в плохом состоянии или получил много урона.")]
    [Min(0f)] public float resourceWeightBoostWhenLowHull = 1.5f;
    [Tooltip("Множитель веса отдыха при высоком напряжении, чтобы дать игроку шанс восстановиться.")]
    [Min(0f)] public float restWeightBoostWhenHighTension = 1.5f;
    [Tooltip("Множитель веса элитного боя при низком корпусе. Значение меньше 1 снижает шанс элитных боев.")]
    [Min(0f)] public float eliteWeightPenaltyWhenLowHull = 0.4f;

    [Header("Низкое напряжение")]
    [Tooltip("Множитель веса элитного боя, когда игрок прошел локацию уверенно: высокий корпус и низкий полученный урон.")]
    [Min(0f)] public float eliteWeightBoostWhenPlayerStrong = 1.3f;
    [Tooltip("Множитель веса обычного боя, когда игрок прошел локацию уверенно.")]
    [Min(0f)] public float combatWeightBoostWhenPlayerStrong = 1.2f;

    [Header("Анти-повтор боев")]
    [Tooltip("Множитель веса обычного и элитного боя после длинной серии боев подряд. Значение меньше 1 снижает повторение боевых узлов.")]
    [Min(0f)] public float combatWeightPenaltyAfterCombatStreak = 0.5f;

    [Header("Рост сложности")]
    [Tooltip("Информационный множитель роста опасности за каждую завершенную локацию. Сейчас доступен директору, но не заменяет выбранный игроком узел.")]
    [Min(0f)] public float difficultyGrowthPerCompletedNode = 0.1f;

    private void OnValidate()
    {
        lowHullThreshold = Mathf.Clamp01(lowHullThreshold);
        heavyDamageThreshold = Mathf.Clamp01(heavyDamageThreshold);
        combatStreakLimit = Mathf.Max(1, combatStreakLimit);
        repairWeightBoostWhenLowHull = Mathf.Max(0f, repairWeightBoostWhenLowHull);
        shopWeightBoostWhenLowHull = Mathf.Max(0f, shopWeightBoostWhenLowHull);
        resourceWeightBoostWhenLowHull = Mathf.Max(0f, resourceWeightBoostWhenLowHull);
        restWeightBoostWhenHighTension = Mathf.Max(0f, restWeightBoostWhenHighTension);
        eliteWeightBoostWhenPlayerStrong = Mathf.Max(0f, eliteWeightBoostWhenPlayerStrong);
        combatWeightBoostWhenPlayerStrong = Mathf.Max(0f, combatWeightBoostWhenPlayerStrong);
        combatWeightPenaltyAfterCombatStreak = Mathf.Max(0f, combatWeightPenaltyAfterCombatStreak);
        eliteWeightPenaltyWhenLowHull = Mathf.Max(0f, eliteWeightPenaltyWhenLowHull);
        difficultyGrowthPerCompletedNode = Mathf.Max(0f, difficultyGrowthPerCompletedNode);
    }
}
