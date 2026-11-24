using UnityEngine;
namespace Lab6
{
    // Animation events live on the shared weapon holder, not on individual weapons.
    public sealed class Lab7FootstepEvents : MonoBehaviour
    {
        public FPSPlayer player;
        public void TriggerFootstepSFX() { if (player) player.TriggerFootstepSFX(); }
    }
}
