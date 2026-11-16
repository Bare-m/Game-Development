using UnityEngine;

public class CoinPickup : MonoBehaviour
{
    [Header("Coin Animation")]
    [SerializeField, Min(0f)] private float spinSpeed = 90f;
    [SerializeField, Min(0f)] private float bobHeight = 0.15f;
    [SerializeField, Min(0f)] private float bobSpeed = 2f;

    private bool collected;
    private Vector3 startLocalPosition;
    private float bobPhase;

    private void Awake()
    {
        Collider pickupCollider = GetComponent<Collider>();
        if (pickupCollider == null)
        {
            pickupCollider = gameObject.AddComponent<SphereCollider>();
        }

        pickupCollider.isTrigger = true;
        startLocalPosition = transform.localPosition;
        bobPhase = Random.value * Mathf.PI * 2f;
    }

    private void Start()
    {
        CoinCounter.EnsureExists();
    }

    private void Update()
    {
        if (collected)
        {
            return;
        }

        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
        transform.localPosition =
            startLocalPosition +
            Vector3.up *
            (Mathf.Sin(Time.time * bobSpeed + bobPhase) * bobHeight);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCollect(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryCollect(collision.collider);
    }

    private void TryCollect(Collider other)
    {
        // Avoid counting the coin more than once before it is destroyed.
        if (collected)
        {
            return;
        }

        if (other.GetComponentInParent<CharacterController>() == null)
        {
            return;
        }

        collected = true;

        // Update the counter.
        CoinCounter.EnsureExists().AddCoin();

        // Remove the coin from the scene.
        Destroy(gameObject);
    }
}