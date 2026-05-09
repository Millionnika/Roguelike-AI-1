using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ObjectiveCollectible : MonoBehaviour
{
    [Header("Параметры контейнера")]
    [Tooltip("Задержка перед активацией сбора, чтобы контейнер не собирался мгновенно при спавне.")]
    [SerializeField, Min(0f)] private float collectDelay = 0.15f;

    private Action<ObjectiveCollectible> onCollected;
    private bool collected;
    private float collectDelayTimer;

    public void Initialize(Action<ObjectiveCollectible> onCollectedCallback)
    {
        onCollected = onCollectedCallback;
        collected = false;
        collectDelayTimer = Mathf.Max(0f, collectDelay);
    }

    private void Reset()
    {
        EnsurePhysics();
    }

    private void Awake()
    {
        EnsurePhysics();
    }

    private void Update()
    {
        if (collectDelayTimer > 0f)
        {
            collectDelayTimer = Mathf.Max(0f, collectDelayTimer - Time.deltaTime);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryCollect(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryCollect(collision != null ? collision.collider : null);
    }

    private void TryCollect(Collider2D other)
    {
        if (collected || collectDelayTimer > 0f || other == null)
        {
            return;
        }

        TeamMember team = other.GetComponentInParent<TeamMember>();
        if (team == null || team.Faction != CombatFaction.Player)
        {
            return;
        }

        collected = true;
        onCollected?.Invoke(this);
        Destroy(gameObject);
    }

    private void EnsurePhysics()
    {
        Collider2D collider = GetComponent<Collider2D>();
        if (collider == null)
        {
            CircleCollider2D circle = gameObject.AddComponent<CircleCollider2D>();
            circle.radius = 0.45f;
            collider = circle;
        }

        collider.isTrigger = true;

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody2D>();
        }

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.simulated = true;
    }
}
