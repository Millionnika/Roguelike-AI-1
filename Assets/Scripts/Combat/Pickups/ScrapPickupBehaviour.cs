using System;
using SpaceFrontier.Player;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ScrapPickupBehaviour : MonoBehaviour
{
    [Header("Параметры Scrap pickup")]
    [Tooltip("Количество Scrap, которое получает игрок при подборе.")]
    [SerializeField, Min(1)] private int amount = 1;
    [Tooltip("Задержка перед включением притяжения к игроку.")]
    [SerializeField, Min(0f)] private float magnetDelay = 0.15f;
    [Tooltip("Радиус, в котором pickup начинает притягиваться к игроку.")]
    [SerializeField, Min(0f)] private float magnetRadius = 2.8f;
    [Tooltip("Скорость подлёта pickup к игроку после входа в радиус притяжения.")]
    [SerializeField, Min(0.1f)] private float magnetSpeed = 8f;

    private RunResources runResources;
    private Transform playerTransform;
    private Action<int, int> collectedCallback;
    private float magnetDelayTimer;
    private bool collected;

    public void Initialize(int scrapAmount, RunResources resources, Transform player, Action<int, int> onCollected = null)
    {
        amount = Mathf.Max(1, scrapAmount);
        runResources = resources;
        playerTransform = player;
        collectedCallback = onCollected;
        magnetDelayTimer = Mathf.Max(0f, magnetDelay);
        collected = false;
    }

    private void Reset()
    {
        EnsurePhysicsSetup();
    }

    private void Awake()
    {
        EnsurePhysicsSetup();
    }

    private void Update()
    {
        if (collected)
        {
            return;
        }

        if (magnetDelayTimer > 0f)
        {
            magnetDelayTimer = Mathf.Max(0f, magnetDelayTimer - Time.deltaTime);
            return;
        }

        if (playerTransform == null)
        {
            return;
        }

        Vector3 toPlayer = playerTransform.position - transform.position;
        float sqrDistance = toPlayer.sqrMagnitude;
        if (sqrDistance > magnetRadius * magnetRadius)
        {
            return;
        }

        float step = Mathf.Max(0.1f, magnetSpeed) * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, playerTransform.position, step);
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
        if (collected || other == null)
        {
            return;
        }

        TeamMember team = other.GetComponentInParent<TeamMember>();
        if (team == null || team.Faction != CombatFaction.Player)
        {
            return;
        }

        collected = true;
        int collectedAmount = Mathf.Max(1, amount);
        runResources?.AddScrap(collectedAmount);
        collectedCallback?.Invoke(collectedAmount, runResources != null ? runResources.Scrap : collectedAmount);
        Destroy(gameObject);
    }

    private void EnsurePhysicsSetup()
    {
        Collider2D collider = GetComponent<Collider2D>();
        if (collider == null)
        {
            CircleCollider2D circle = gameObject.AddComponent<CircleCollider2D>();
            circle.radius = 0.18f;
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
