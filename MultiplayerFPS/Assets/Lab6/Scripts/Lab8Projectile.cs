using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace Lab6
{
    [RequireComponent(typeof(Rigidbody), typeof(PhotonView))]
    public sealed class Lab8Projectile : MonoBehaviourPun, IPunInstantiateMagicCallback
    {
        public float fireForce = 500f;
        public float explosionRadius = 5f;
        public int damage = 30;
        public string explosionPrefabName = "Lab8Explosion";

        public int ShooterActor { get; private set; }
        public int ShooterViewId { get; private set; }
        public bool HasExploded { get; private set; }
        public static int ActiveProjectileCount => active.Count;
        public static int ExplosionCount { get; private set; }
        static readonly HashSet<Lab8Projectile> active = new HashSet<Lab8Projectile>();

        Rigidbody body;
        Collider[] projectileColliders;
        bool initialized;
        readonly HashSet<int> damagedPlayers = new HashSet<int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCounters()
        {
            active.Clear();
            ExplosionCount = 0;
        }

        public static GameObject Spawn(string prefabName, Vector3 position, Quaternion rotation,
            int shooterActor, int shooterViewId)
        {
            if (!PhotonNetwork.InRoom || shooterActor != PhotonNetwork.LocalPlayer.ActorNumber || AssignmentMatch.IsFinished || FPSRoom.Instance.MapLoading
                || string.IsNullOrEmpty(prefabName) || shooterActor <= 0 || shooterViewId <= 0
                || !Finite(position) || !Finite(rotation)) return null;
            var shooter = PhotonView.Find(shooterViewId);
            if (!shooter || !shooter.IsMine) return null;
            return PhotonNetwork.Instantiate(prefabName, position, rotation, 0,
                new object[] { shooterActor, shooterViewId });
        }

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            projectileColliders = GetComponentsInChildren<Collider>();
        }

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        public void OnPhotonInstantiate(PhotonMessageInfo info)
        {
            var data = photonView.InstantiationData;
            if (data != null && data.Length >= 2 && data[0] is int actor && data[1] is int view)
            {
                if (info.Sender == null || actor != info.Sender.ActorNumber) return;
                var shooter = PhotonView.Find(view);
                if (!shooter || shooter.Owner != info.Sender) return;
                ShooterActor = actor;
                ShooterViewId = view;
            }
            InitializePhysics();
        }

        void Start()
        {
            if (!initialized) InitializePhysics();
        }

        void InitializePhysics()
        {
            if (initialized) return;
            initialized = true;
            // E16 simulates the same initial force on every peer; only its owner deals damage.
            bool valid = PhotonNetwork.InRoom && ShooterActor > 0 && ShooterViewId > 0;
            body.isKinematic = !valid;
            foreach (var collider in projectileColliders) collider.enabled = valid;
            if (!valid) return;

            var shooter = PhotonView.Find(ShooterViewId);
            if (shooter)
                foreach (var collider in shooter.GetComponentsInChildren<Collider>())
                    foreach (var projectileCollider in projectileColliders)
                        Physics.IgnoreCollision(projectileCollider, collider);

            // E16's final direction fix uses AddForce's default Force mode, not Impulse.
            body.AddForce(transform.forward * fireForce, ForceMode.Force);
        }

        void Update()
        {
            if (!PhotonNetwork.InRoom) return;
            if (photonView.IsMine)
            {
                // Cleanup only: a missed projectile is not a timed explosive.
                if (AssignmentMatch.IsFinished || FPSRoom.Instance.MapLoading || PhotonNetwork.CurrentRoom.GetPlayer(ShooterActor)==null || transform.position.y < -30f)
                    PhotonNetwork.Destroy(gameObject);
                return;
            }
        }

        void OnCollisionEnter(Collision collision) => Detonate();

        // One authority and one guard for both physics callbacks and runtime acceptance checks.
        public bool Detonate()
        {
            if (!PhotonNetwork.InRoom || !photonView.IsMine
                || AssignmentMatch.IsFinished || HasExploded) return false;
            if (FPSRoom.Instance.MapLoading || PhotonNetwork.CurrentRoom.GetPlayer(ShooterActor)==null)
            { PhotonNetwork.Destroy(gameObject); return false; }
            HasExploded = true;
            ExplosionCount++;
            foreach (var collider in projectileColliders) collider.enabled = false;

            if (!string.IsNullOrEmpty(explosionPrefabName))
                PhotonNetwork.Instantiate(explosionPrefabName, transform.position, Quaternion.identity);

            var attacker = PhotonNetwork.CurrentRoom.GetPlayer(ShooterActor);
            var players = new HashSet<int>();
            var bots = new HashSet<int>();
            foreach (var collider in Physics.OverlapSphere(transform.position, explosionRadius,
                ~0, QueryTriggerInteraction.Ignore))
            {
                var player = collider.GetComponentInParent<FPSPlayer>();
                if (player)
                {
                    var view = player.photonView;
                    if (view.ViewID == ShooterViewId || view.OwnerActorNr == ShooterActor
                        || !players.Add(view.ViewID) || player.Dead) continue;
                    // E16 awards the full damage value even when the target has less HP remaining.
                    player.photonView.RPC("TakeProjectileDamage", RpcTarget.All, photonView.ViewID);
                    continue;
                }
                var bot = collider.GetComponentInParent<OfflineBot>();
                if (bot && bots.Add(bot.GetInstanceID()) && bot.Health > 0)
                    bot.TakeDamage(damage, attacker);
            }
            PhotonNetwork.Destroy(gameObject);
            return true;
        }

        public bool ValidateDamage(FPSPlayer target, Photon.Realtime.Player sender)
        {
            if (sender != photonView.Owner || sender.ActorNumber != ShooterActor || target.photonView.OwnerActorNr == ShooterActor
                || target.Dead || damagedPlayers.Contains(target.photonView.ViewID)) return false;
            // Peers simulate independently as in E16; allow one radius of network/physics drift.
            if (Vector3.Distance(target.transform.position + Vector3.up, transform.position) > explosionRadius * 2) return false;
            return damagedPlayers.Add(target.photonView.ViewID);
        }

        static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        static bool Finite(Quaternion value) => float.IsFinite(value.x) && float.IsFinite(value.y)
            && float.IsFinite(value.z) && float.IsFinite(value.w);
    }
}
