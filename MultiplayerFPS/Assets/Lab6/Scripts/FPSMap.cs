using UnityEngine;

namespace Lab6
{
    // A map owns geometry and spawn references, never networking/UI managers.
    public sealed class FPSMap : MonoBehaviour
    {
        [Range(1, 2)] public int mapIndex = 1;
        public Transform[] spawnPoints;
        public string DisplayName => FPSRoom.MapName(mapIndex);
    }
}
