using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

namespace Lab6
{
    [RequireComponent(typeof(NavMeshAgent),typeof(CapsuleCollider))]
    public sealed class OfflineBot : MonoBehaviour
    {
        public GameObject body;
        public TMP_Text nickname;
        public AudioSource shotSource;
        public Material traceMaterial;
        public int Health { get; private set; } = 100;
        public int ShotsFired { get; private set; }
        public int RespawnCount { get; private set; }
        public int BotIndex { get; private set; }
        NavMeshAgent agent;
        CapsuleCollider hitbox;
        float nextThink, nextShot;
        int patrolPoint;

        public void Initialize(int index)
        {
            BotIndex=index; name=OfflineBots.Records[index].Name;
            if(nickname) nickname.text=name;
            nextShot=Time.time+2;
        }
        void Awake()
        {
            agent=GetComponent<NavMeshAgent>(); hitbox=GetComponent<CapsuleCollider>();
        }
        void Update()
        {
            var room=FPSRoom.Instance;
            if (!room || !PhotonNetwork.OfflineMode || !OfflineBots.Enabled) { Destroy(gameObject); return; }
            if(nickname && Camera.main) nickname.transform.rotation=Camera.main.transform.rotation;
            if(Health<=0 || !agent.enabled || !agent.isOnNavMesh) return;
            bool stopped=room.Paused || room.MapLoading || AssignmentMatch.IsFinished;
            if(stopped) { agent.isStopped=true; agent.velocity=Vector3.zero; return; }
            agent.isStopped=false;
            if(Time.time<nextThink) return;
            nextThink=Time.time+.2f;
            var target=room.LocalPlayer;
            if(target && !target.Dead)
            {
                var eye=transform.position+Vector3.up*1.4f;
                var targetPoint=target.transform.position+Vector3.up;
                var delta=targetPoint-eye;
                bool visible=false;
                float nearest=delta.magnitude;
                foreach(var hit in Physics.RaycastAll(eye,delta.normalized,nearest,~0,QueryTriggerInteraction.Ignore))
                {
                    if(hit.collider.transform.IsChildOf(transform) || hit.distance>nearest) continue;
                    nearest=hit.distance;
                    visible=hit.collider.GetComponentInParent<FPSPlayer>()==target;
                }
                if(visible && delta.magnitude<=24)
                {
                    agent.isStopped=delta.magnitude<10;
                    Vector3 facing=target.transform.position-transform.position; facing.y=0;
                    if(facing.sqrMagnitude>.01f) transform.rotation=Quaternion.LookRotation(facing);
                    if(Time.time>=nextShot)
                    {
                        nextShot=Time.time+1.1f; ShotsFired++;
                        shotSource.Play();
                        var beam=new GameObject("BotShot").AddComponent<LineRenderer>();
                        beam.sharedMaterial=traceMaterial; beam.positionCount=2; beam.startWidth=.025f; beam.endWidth=.008f;
                        beam.SetPosition(0,eye); beam.SetPosition(1,targetPoint); Destroy(beam.gameObject,.06f);
                        int damage=Mathf.Min(10,target.Health); bool killed=target.Health<=10;
                        var record=OfflineBots.Records[BotIndex]; record.Score+=1+damage+(killed?100:0);
                        if(killed) { record.Kills++; Lab7Stats.RecordDeath(PhotonNetwork.LocalPlayer); }
                        OfflineBots.Records[BotIndex]=record;
                        target.photonView.RPC("ApplyHealth",RpcTarget.All,target.Health-damage);
                    }
                }
                if(!agent.isStopped && NavMesh.SamplePosition(target.transform.position,out var nav,5,NavMesh.AllAreas))
                    agent.SetDestination(nav.position);
            }
            else if(!agent.pathPending && (!agent.hasPath || agent.remainingDistance<1))
            {
                var points=room.spawnPoints;
                if(points.Length>0 && NavMesh.SamplePosition(points[patrolPoint++%points.Length].position,out var nav,5,NavMesh.AllAreas)) agent.SetDestination(nav.position);
            }
        }

        public void TakeDamage(int amount,Player attacker)
        {
            if(!PhotonNetwork.OfflineMode || !PhotonNetwork.IsMasterClient || AssignmentMatch.IsFinished || Health<=0 || amount<=0 || attacker!=PhotonNetwork.LocalPlayer) return;
            bool killed=amount>=Health;
            Lab7Stats.RecordBotDamage(attacker,amount,killed);
            Health=Mathf.Max(0,Health-amount);
            if(!killed) return;
            var record=OfflineBots.Records[BotIndex]; record.Deaths++; OfflineBots.Records[BotIndex]=record;
            agent.isStopped=true; agent.enabled=false; hitbox.enabled=false; body.SetActive(false);
            if(nickname) nickname.gameObject.SetActive(false);
            StartCoroutine(Respawn());
        }

        IEnumerator Respawn()
        {
            yield return new WaitForSeconds(2);
            var room=FPSRoom.Instance;
            if(!room || room.MapLoading || AssignmentMatch.IsFinished || !PhotonNetwork.InRoom) yield break;
            var points=room.spawnPoints;
            var point=points[(BotIndex+RespawnCount+1)%points.Length];
            if(!NavMesh.SamplePosition(point.position,out var nav,5,NavMesh.AllAreas)) yield break;
            transform.SetPositionAndRotation(nav.position,point.rotation);
            Health=100; RespawnCount++; hitbox.enabled=true; body.SetActive(true);
            if(nickname) nickname.gameObject.SetActive(true);
            agent.enabled=true; agent.Warp(nav.position); nextShot=Time.time+2;
        }
    }
}
