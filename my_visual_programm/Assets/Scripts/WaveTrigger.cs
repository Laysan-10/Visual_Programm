using UnityEngine;

public class WaveTrigger : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<PlayerController>(out PlayerController player))
            player.OpenWaves();
    }
}
