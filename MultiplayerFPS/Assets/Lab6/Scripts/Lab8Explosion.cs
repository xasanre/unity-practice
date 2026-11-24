using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace Lab6
{
    [RequireComponent(typeof(PhotonView))]
    public sealed class Lab8Explosion : MonoBehaviourPun
    {
        public float lifetime = 3f;
        public static int ActiveExplosionCount => active.Count;
        static readonly HashSet<Lab8Explosion> active = new HashSet<Lab8Explosion>();
        float expiresAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCounters() => active.Clear();

        void OnEnable()
        {
            active.Add(this);
            expiresAt = Time.time + lifetime;
        }

        void OnDisable() => active.Remove(this);

        void Start()
        {
            foreach (var particles in GetComponentsInChildren<ParticleSystem>())
                particles.Play(false);
        }

        void Update()
        {
            if (Time.time < expiresAt || !photonView.IsMine || !PhotonNetwork.InRoom) return;
            PhotonNetwork.Destroy(gameObject);
        }
    }
}
