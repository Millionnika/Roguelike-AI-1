using UnityEngine;

namespace SpaceFrontier.Player
{
    public class PlayerShip : MonoBehaviour
    {
        [Header("Renderers")]
        public SpriteRenderer bodyRenderer;
        public SpriteRenderer auraRenderer;
        public SpriteRenderer thrusterRenderer;

        [Header("Visual Settings")]
        public Color baseBodyColor = Color.white;
        public Color baseAuraColor = Color.white;

        public float hitFlashTimer;
        public float thrusterPulse;

        public void SetVisuals(Color bodyColor, Color auraColor)
        {
            baseBodyColor = bodyColor;
            baseAuraColor = auraColor;
            if (bodyRenderer != null) bodyRenderer.color = bodyColor;
            if (auraRenderer != null) auraRenderer.color = auraColor;
        }
    }
}