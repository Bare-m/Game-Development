using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class Checkpoint : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;

    private PlayerController activatedForPlayer;

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerController player =
            other.GetComponentInParent<PlayerController>();

        if (player == null || player == activatedForPlayer)
        {
            return;
        }

        Vector3 position = spawnPoint != null
            ? spawnPoint.position
            : player.transform.position;
        Quaternion rotation = spawnPoint != null
            ? spawnPoint.rotation
            : player.transform.rotation;

        player.SetCheckpoint(position, rotation);
        activatedForPlayer = player;
    }
}
