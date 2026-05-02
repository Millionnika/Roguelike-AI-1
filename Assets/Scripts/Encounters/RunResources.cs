using UnityEngine;

[DisallowMultipleComponent]
public sealed class RunResources : MonoBehaviour
{
    [Header("Ресурсы забега")]
    [Tooltip("Текущий объём Scrap в рамках текущего забега.")]
    [SerializeField, Min(0)] private int scrap;

    public int Scrap => scrap;

    public void AddScrap(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        scrap += amount;
    }

    public bool SpendScrap(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (scrap < amount)
        {
            return false;
        }

        scrap -= amount;
        return true;
    }

    public void ResetRunResources()
    {
        scrap = 0;
    }
}
