using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

namespace Lab6
{
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider), typeof(PhotonView))]
    public sealed class FPSPlayer : MonoBehaviourPunCallbacks, IPunObservable
    {
        public Camera viewCamera;
        public Camera weaponCamera;
        public GameObject[] firstPersonWeapons = new GameObject[0], thirdPersonWeapons = new GameObject[0];
        public WeaponSpec[] weaponSpecs = new WeaponSpec[0];
        public Animation weaponAnimation, reloadAnimation;
        public AudioSource footstepSource, gunShootSource;
        public AudioClip[] allGunShootSFX = new AudioClip[0];
        public int SelectedWeapon { get; private set; }
        int weaponRevision;
        public int WeaponCount => weaponSpecs != null && weaponSpecs.Length > 0 ? weaponSpecs.Length : Mathf.Max(1, firstPersonWeapons.Length);
        public WeaponSpec CurrentWeapon { get { EnsureWeapons(); return runtimeSpecs[SelectedWeapon]; } }
        public int ShotSoundCount { get; private set; }
        public int LastPelletCount { get; private set; }
        public int PelletEffectCount { get; private set; }
        public int FootstepSoundCount { get; private set; }
        public Transform weapon, recoilPivot;
        public float recoilUp=.02f, recoilBack=.08f, recoilPercent=.3f, recoverPercent=.7f;
        Vector3 recoilVelocity, recoilRest;
        bool recoiling, recovering;
        public GameObject body;
        public GameObject thirdPersonWeapon;
        public TMP_Text nicknameLabel;
        public Transform nicknameBillboard;
        public AudioClip shotSound, reloadSound;
        public Material effectMaterial;
        public int Health { get; private set; } = 100;
        public WeaponState Gun { get { EnsureWeapons(); return guns[SelectedWeapon]; } }
        public bool IsLocal => photonView.IsMine;
        public bool Dead => Health <= 0;
        public bool Aiming { get; private set; }
        public float ReloadProgress => Gun.Reloading ? 1f - Mathf.Clamp01((float)((Gun.ReloadUntil - PhotonNetwork.Time) / Gun.ReloadDuration)) : 0;
        WeaponState[] guns, authoritativeGuns;
        WeaponSpec[] runtimeSpecs, appliedSpecs;
        double nextFootstep;
        float drawUntil, nextFallRequest;
        public float walkSpeed=4f, sprintSpeed=14f, maxVelocityChange=10f, airControl=.5f, jumpHeight=5f;
        public float swayClamp=.09f, swaySmoothing=3f;
        public bool Grounded => grounded;
        Rigidbody rb;
        Vector2 movementInput;
        bool grounded, sprinting, jumping;
        AudioSource audioSource;
        Vector3 weaponRest;
        int nextShotId, acceptedShotId, acceptedWeapon;
        int[] acceptedTargets = new int[0];
        int healthVersion, nextHealthVersion;
        bool confirmedDeath;
        float pitch, remotePitch, lookYaw;
        Vector2 smoothMouse;
        Quaternion initialLookRotation, initialBodyRotation;

        void EnsureWeapons()
        {
            if (guns != null && guns.Length == WeaponCount && appliedSpecs == weaponSpecs) return;
            appliedSpecs = weaponSpecs;
            runtimeSpecs = new WeaponSpec[WeaponCount];
            guns = new WeaponState[WeaponCount];
            authoritativeGuns = new WeaponState[WeaponCount];
            for (int i = 0; i < WeaponCount; i++)
            {
                var spec = weaponSpecs != null && i < weaponSpecs.Length ? weaponSpecs[i] : null;
                runtimeSpecs[i] = spec ?? new WeaponSpec
                {
                    shootSFXIndex = i, recoilUp = recoilUp, recoilBack = recoilBack,
                    recoilPercent = recoilPercent, recoverPercent = recoverPercent
                };
                guns[i] = runtimeSpecs[i].CreateState();
                authoritativeGuns[i] = runtimeSpecs[i].CreateState();
                if (reloadAnimation && runtimeSpecs[i].reloadClip)
                    reloadAnimation.AddClip(runtimeSpecs[i].reloadClip, runtimeSpecs[i].reloadClip.name);
            }
            SelectedWeapon = Mathf.Clamp(SelectedWeapon, 0, WeaponCount - 1);
        }

        void Awake()
        {
            EnsureWeapons();
            rb = GetComponent<Rigidbody>();
            audioSource = GetComponent<AudioSource>();
            // Only the owning player's Start enables its camera/audio listener.
            viewCamera.gameObject.SetActive(false);
            initialLookRotation = viewCamera.transform.localRotation;
            initialBodyRotation = transform.localRotation;
            weaponRest = weapon.localPosition;
            if(recoilPivot) recoilRest=recoilPivot.localPosition;
        }

        void Start()
        {
            if (!PhotonNetwork.InRoom) { Destroy(gameObject); return; }
            viewCamera.gameObject.SetActive(IsLocal);
            body.SetActive(!IsLocal);
            thirdPersonWeapon.SetActive(!IsLocal);
            nicknameLabel.gameObject.SetActive(!IsLocal);
            nicknameLabel.text = photonView.Owner != null ? photonView.Owner.NickName : "Player";
            rb.isKinematic = !IsLocal;
            audioSource.spatialBlend = 1;
            ApplyWeaponVisuals();
            if(IsLocal)
            {
                PlayDraw();
                photonView.RPC(nameof(SetNickname),RpcTarget.AllBuffered,photonView.Owner.NickName);
            }
            GetComponent<CapsuleCollider>().enabled = true;
        }

        void Update()
        {
            EnsureWeapons();
            foreach (var gun in guns) gun.Tick(PhotonNetwork.Time);
            if (!IsLocal)
            {
                thirdPersonWeapon.transform.localRotation = Quaternion.Euler(remotePitch, 0, 0);
                var cam = Camera.main;
                if (cam && nicknameBillboard) nicknameBillboard.LookAt(cam.transform);
                return;
            }
            UpdateReloadAnimation();
            var room = FPSRoom.Instance;
            if (!Dead && room && room.LocalPlayer == this && transform.position.y < -12 && !room.MapLoading &&
                !AssignmentMatch.IsFinished && Time.unscaledTime >= nextFallRequest)
            {
                // A request can reach the Master before the transform sample; retry without flooding reliable RPCs.
                nextFallRequest = Time.unscaledTime + .5f;
                photonView.RPC(nameof(RequestFall), RpcTarget.MasterClient);
            }
            if (Dead || !room || room.InputBlocked || !Application.isFocused)
            {
                movementInput = Vector2.zero;
                smoothMouse = Vector2.zero;
                sprinting = jumping = Aiming = false;
                return;
            }
            Aiming = Input.GetMouseButton(1);
            float mouseX = Input.GetAxisRaw("Mouse X"), mouseY = Input.GetAxisRaw("Mouse Y");
            ApplyMouseLook(new Vector2(mouseX, mouseY));
            viewCamera.fieldOfView = Mathf.Lerp(viewCamera.fieldOfView, Aiming ? 48 : 60, Time.deltaTime * 12);
            if (weaponCamera) weaponCamera.fieldOfView = viewCamera.fieldOfView;
            for (int i=0;i<9;i++) if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+i))) SelectWeapon(i);
            float wheel=Input.GetAxis("Mouse ScrollWheel");
            if (wheel != 0) ScrollWeapon(wheel>0 ? 1 : -1);
            movementInput=new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical")).normalized;
            sprinting=Input.GetButton("Sprint");
            jumping=Input.GetButton("Jump");
            if (weaponAnimation && Time.time >= drawUntil)
            {
                string clip=grounded && movementInput.sqrMagnitude>.01f ? "Walk" : "Idle";
                if (!weaponAnimation.IsPlaying(clip)) weaponAnimation.Play(clip);
            }
            if (Input.GetKeyDown(KeyCode.R)) TryReload();
            if (Input.GetButton("Fire1") && Time.time >= drawUntil)
                TryFire(viewCamera.transform.position, viewCamera.transform.forward);
            Vector3 sway = new Vector3(-Mathf.Clamp(mouseX,-swayClamp,swayClamp),-Mathf.Clamp(mouseY,-swayClamp,swayClamp),0);
            Vector3 target = (Aiming ? new Vector3(0.06f, -0.18f, 0.40f) : weaponRest) + sway;
            weapon.localPosition = Vector3.Lerp(weapon.localPosition, target, Time.deltaTime * swaySmoothing);
        }

        // Blocking new inputs must not freeze the kick and recovery of an already fired shot.
        void LateUpdate()
        {
            if(IsLocal && recoilPivot && (recoiling || recovering))
            {
                var spec = CurrentWeapon;
                Vector3 goal=recoiling ? recoilRest+new Vector3(0,spec.recoilUp,-spec.recoilBack) : recoilRest;
                float duration=(float)Gun.ShotInterval*(recoiling ? spec.recoilPercent : spec.recoverPercent);
                recoilPivot.localPosition=Vector3.SmoothDamp(recoilPivot.localPosition,goal,ref recoilVelocity,duration);
                // At high frame rates SmoothDamp can round to the same float just short of the goal.
                if((recoilPivot.localPosition-goal).sqrMagnitude <= .001f*.001f)
                {
                    recoilPivot.localPosition=goal;
                    recoilVelocity=Vector3.zero;
                    if(recoiling) { recoiling=false; recovering=true; }
                    else recovering=false;
                }
            }
        }

        void ApplyMouseLook(Vector2 raw)
        {
            // E02/E03 use the supplied MouseLook: sensitivity 2, smoothing 3, vertical clamp 180.
            float sensitivity = Aiming ? 1.1f : 2f;
            smoothMouse = Vector2.Lerp(smoothMouse, raw * (sensitivity * 3f), 1f / 3f);
            lookYaw += smoothMouse.x;
            pitch = Mathf.Clamp(pitch - smoothMouse.y, -90, 90);
            viewCamera.transform.localRotation = Quaternion.AngleAxis(pitch, initialLookRotation * Vector3.right) * initialLookRotation;
            transform.localRotation = Quaternion.AngleAxis(lookYaw, Vector3.up) * initialBodyRotation;
        }

        public bool TryReload()
        {
            double sentAt = PhotonNetwork.Time;
            if (!IsLocal || Dead || AssignmentMatch.IsFinished || FPSRoom.Instance.MapLoading || !Gun.Reload(sentAt)) return false;
            UpdateReloadAnimation();
            if (audioSource && reloadSound) audioSource.PlayOneShot(reloadSound, .6f);
            photonView.RPC(nameof(RequestReload), RpcTarget.All, SelectedWeapon, sentAt);
            return true;
        }

        void UpdateReloadAnimation()
        {
            if(!reloadAnimation) return;
            string clipName = CurrentWeapon.reloadClip ? CurrentWeapon.reloadClip.name : "Reload";
            if(Gun.Reloading)
            {
                if(reloadAnimation[clipName] != null && !reloadAnimation.IsPlaying(clipName))
                {
                    reloadAnimation.Play(clipName);
                    reloadAnimation[clipName].speed = reloadAnimation[clipName].length / (float)Gun.ReloadDuration;
                    reloadAnimation[clipName].normalizedTime = Mathf.Clamp01(1f - (float)((Gun.ReloadUntil - PhotonNetwork.Time) / Gun.ReloadDuration));
                    reloadAnimation.Sample();
                }
            }
            else
            {
                reloadAnimation.Stop();
                reloadAnimation.transform.localPosition=Vector3.zero;
                reloadAnimation.transform.localRotation=Quaternion.identity;
            }
        }

        [PunRPC] void SetNickname(string nickname,PhotonMessageInfo info)
        {
            if (info.Sender!=photonView.Owner || string.IsNullOrEmpty(nickname) || nickname.Length>20) return;
            nicknameLabel.text=nickname;
        }

        void OnTriggerStay(Collider other)
        {
            if (IsLocal && !other.isTrigger && !other.transform.IsChildOf(transform)) grounded=true;
        }

        void FixedUpdate()
        {
            if (!IsLocal || Dead || FPSRoom.Instance==null) return;
            if (FPSRoom.Instance.InputBlocked)
            {
                movementInput = Vector2.zero;
                jumping = sprinting = false;
                if (!rb.isKinematic) rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
                grounded = false;
                return;
            }
            if (grounded && jumping)
                rb.linearVelocity=new Vector3(rb.linearVelocity.x,jumpHeight,rb.linearVelocity.z);
            else if (movementInput.magnitude>.5f)
            {
                float speed=sprinting ? sprintSpeed : walkSpeed;
                rb.AddForce(CalculateMovement(grounded ? speed : speed*airControl),ForceMode.VelocityChange);
            }
            else
            {
                Vector3 v=rb.linearVelocity;
                rb.linearVelocity=new Vector3(v.x*.2f*Time.fixedDeltaTime,v.y,v.z*.2f*Time.fixedDeltaTime);
            }
            grounded=false;
        }

        Vector3 CalculateMovement(float speed)
        {
            Vector3 targetVelocity=transform.TransformDirection(new Vector3(movementInput.x,0,movementInput.y))*speed;
            Vector3 change=targetVelocity-rb.linearVelocity;
            change.x=Mathf.Clamp(change.x,-maxVelocityChange,maxVelocityChange);
            change.z=Mathf.Clamp(change.z,-maxVelocityChange,maxVelocityChange);
            change.y=0;
            return change;
        }

        public bool SelectWeapon(int index)
        {
            if (!IsLocal || Dead) return false;
            index=Mathf.Clamp(index,0,WeaponCount-1);
            if (index==SelectedWeapon) return false;
            SelectedWeapon=index;
            ApplyWeaponVisuals();
            recoiling=recovering=false;
            recoilVelocity=Vector3.zero;
            if(recoilPivot) recoilPivot.localPosition=recoilRest;
            if(reloadAnimation)
            {
                reloadAnimation.Stop();
                reloadAnimation.transform.localPosition=Vector3.zero;
                reloadAnimation.transform.localRotation=Quaternion.identity;
                UpdateReloadAnimation();
            }
            PlayDraw();
            weaponRevision++;
            photonView.RPC(nameof(SetTPWeapon),RpcTarget.All,index,weaponRevision);
            return true;
        }

        public void ScrollWeapon(int direction) => SelectWeapon((SelectedWeapon+(direction>=0 ? 1 : WeaponCount-1))%WeaponCount);

        void PlayDraw()
        {
            if (!weaponAnimation || weaponAnimation["Draw"] == null) return;
            weaponAnimation.Stop();
            weaponAnimation.Play("Draw");
            drawUntil = Time.time + weaponAnimation["Draw"].length;
        }

        [PunRPC] void SetTPWeapon(int index,int revision,PhotonMessageInfo info)
        {
            if (info.Sender!=photonView.Owner || index<0 || index>=WeaponCount || revision<weaponRevision || Dead) return;
            weaponRevision=revision;
            SelectedWeapon=index;
            ApplyWeaponVisuals();
        }

        void ApplyWeaponVisuals()
        {
            for(int i=0;i<firstPersonWeapons.Length;i++) firstPersonWeapons[i].SetActive(i==SelectedWeapon);
            for(int i=0;i<thirdPersonWeapons.Length;i++) thirdPersonWeapons[i].SetActive(i==SelectedWeapon);
        }

        public void TriggerFootstepSFX()
        {
            if (!IsLocal || Dead || !grounded || FPSRoom.Instance==null || FPSRoom.Instance.InputBlocked) return;
            if (new Vector2(rb.linearVelocity.x,rb.linearVelocity.z).sqrMagnitude<.01f) return;
            photonView.RPC(nameof(FootstepSound),RpcTarget.All);
        }

        [PunRPC] void FootstepSound(PhotonMessageInfo info)
        {
            if (info.Sender!=photonView.Owner || Dead || !footstepSource || PhotonNetwork.Time<nextFootstep) return;
            nextFootstep=PhotonNetwork.Time+.15;
            footstepSource.pitch=Random.Range(.7f,1.2f); footstepSource.volume=Random.Range(.2f,.35f);
            footstepSource.Play(); FootstepSoundCount++;
        }

        [PunRPC] void RequestReload(int weaponIndex, double sentAt, PhotonMessageInfo info)
        {
            if (info.Sender != photonView.Owner || AssignmentMatch.IsFinished || FPSRoom.Instance.MapLoading
                || weaponIndex < 0 || weaponIndex >= WeaponCount || !ValidEventTime(sentAt)) return;
            EnsureWeapons();
            authoritativeGuns[weaponIndex].Reload(sentAt);
        }

        public bool TryFire(Vector3 origin, Vector3 direction)
        {
            double sentAt = PhotonNetwork.Time;
            if (!IsLocal || Dead || !PhotonNetwork.InRoom || AssignmentMatch.IsFinished || FPSRoom.Instance.MapLoading
                || !Finite(origin) || !Finite(direction) || Mathf.Abs(direction.magnitude - 1) > .05f
                || Vector3.Distance(origin, viewCamera.transform.position) > 3 || !Gun.Fire(sentAt)) return false;
            var spec = CurrentWeapon;
            int shot = ++nextShotId;
            direction.Normalize();
            if (spec.projectile)
            {
                photonView.RPC(nameof(ShotEffects), RpcTarget.All, new Vector3[0], new Vector3[0], new byte[0],
                    new int[0], SelectedWeapon, shot, sentAt, origin);
                Lab8Projectile.Spawn(spec.projectilePrefab, origin + direction, Quaternion.LookRotation(direction),
                    photonView.Owner.ActorNumber, photonView.ViewID);
                return true;
            }
            recoiling = true; recovering = false;
            int pellets = Mathf.Clamp(spec.pelletsCount, 1, 32);
            var points = new Vector3[pellets];
            var normals = new Vector3[pellets];
            var flags = new byte[pellets];
            var targets = new int[pellets];
            var victims = new FPSPlayer[pellets];
            var bots = new OfflineBot[pellets];
            for (int i = 0; i < pellets; i++)
            {
                // E15 casts on the shooting client, with world-XY spread from insideUnitCircle.
                Vector3 offset = (Vector3)Random.insideUnitCircle * spec.sprayMultiplier;
                Vector3 pelletDirection = (direction + offset).normalized;
                points[i] = origin + pelletDirection * 100;
                normals[i] = -pelletDirection;
                float nearest = 100;
                foreach (var hit in Physics.RaycastAll(origin, pelletDirection, 100, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.transform.IsChildOf(transform) || hit.distance >= nearest) continue;
                    nearest = hit.distance; points[i] = hit.point; normals[i] = hit.normal; flags[i] = 1;
                    victims[i] = hit.collider.GetComponentInParent<FPSPlayer>();
                    bots[i] = hit.collider.GetComponentInParent<OfflineBot>();
                    if (hit.collider.GetComponentInParent<TargetDummy>()) flags[i] |= 2;
                }
                if (victims[i] && !victims[i].Dead) { targets[i] = victims[i].photonView.ViewID; flags[i] |= 2; }
                else if (bots[i] && bots[i].Health > 0) flags[i] |= 2;
            }
            // Reliable per-sender ordering lets receivers validate each damage RPC against this shot.
            photonView.RPC(nameof(ShotEffects), RpcTarget.All, points, normals, flags, targets, SelectedWeapon, shot, sentAt, origin);
            for (int i = 0; i < pellets; i++)
            {
                if (targets[i] != 0)
                    victims[i].photonView.RPC(nameof(TakeDamage), RpcTarget.All, spec.damage, photonView.ViewID, shot, i);
                else if (bots[i] && bots[i].Health > 0) bots[i].TakeDamage(spec.damage, photonView.Owner);
            }
            return true;
        }

        [PunRPC] void TakeDamage(int amount, int shooterViewId, int shot, int pellet, PhotonMessageInfo info)
        {
            var shooterView = PhotonView.Find(shooterViewId);
            var shooter = shooterView ? shooterView.GetComponent<FPSPlayer>() : null;
            if (!shooter || info.Sender != shooterView.Owner || shooterView.Owner == photonView.Owner
                || shot != shooter.acceptedShotId || pellet < 0 || pellet >= shooter.acceptedTargets.Length
                || shooter.acceptedTargets[pellet] != photonView.ViewID
                || amount != shooter.runtimeSpecs[shooter.acceptedWeapon].damage) return;
            shooter.acceptedTargets[pellet] = 0;
            ApplyTutorialDamage(info.Sender, amount);
        }

        [PunRPC] void TakeProjectileDamage(int projectileViewId, PhotonMessageInfo info)
        {
            var view = PhotonView.Find(projectileViewId);
            var projectile = view ? view.GetComponent<Lab8Projectile>() : null;
            if (!projectile || !projectile.ValidateDamage(this, info.Sender)) return;
            ApplyTutorialDamage(info.Sender, projectile.damage);
        }

        void ApplyTutorialDamage(Player attacker, int amount)
        {
            if (!IsLocal || Dead || amount <= 0 || AssignmentMatch.IsFinished || FPSRoom.Instance.MapLoading) return;
            SetHealth(Mathf.Max(0, Health - amount));
            // The victim confirms an absolute HP/version so late-join snapshots cannot double-apply damage.
            photonView.RPC(nameof(ConfirmDamage), RpcTarget.All, Health, amount, attacker.ActorNumber, ++nextHealthVersion);
        }

        [PunRPC] void ConfirmDamage(int health, int amount, int attackerActor, int version, PhotonMessageInfo info)
        {
            if (info.Sender != photonView.Owner || version <= healthVersion || health < 0 || health > 100
                || amount <= 0 || amount > 1000 || !PhotonNetwork.InRoom) return;
            var attacker = PhotonNetwork.CurrentRoom.GetPlayer(attackerActor);
            if (attackerActor == photonView.OwnerActorNr) return;
            healthVersion = version;
            if (attacker != null) Lab7Stats.RecordDamage(attacker, photonView.Owner, amount, health == 0 && !confirmedDeath);
            confirmedDeath |= health == 0;
            if (!IsLocal) SetHealth(health);
        }

        static bool ValidEventTime(double value) => !double.IsNaN(value) && !double.IsInfinity(value)
            && value <= PhotonNetwork.Time + .5 && value >= PhotonNetwork.Time - 5;

        // Trusted non-weapon damage entry for offline fixtures and environmental checks.
        // RpcTarget.All executes locally before the next pellet, so Dead prevents duplicate kills.
        public bool ApplyDamageFromMaster(Player attacker, int amount)
        {
            if (!PhotonNetwork.IsMasterClient || AssignmentMatch.IsFinished || FPSRoom.Instance.MapLoading || Dead || amount <= 0) return false;
            bool killed = amount >= Health;
            Lab7Stats.RecordDamage(attacker, photonView.Owner, amount, killed);
            photonView.RPC(nameof(ApplyHealth), RpcTarget.All, Mathf.Max(0, Health - amount));
            return true;
        }

        static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);

        [PunRPC] void RequestFall(PhotonMessageInfo info)
        {
            if (PhotonNetwork.IsMasterClient && info.Sender == photonView.Owner && transform.position.y < -10 && !Dead
                && FPSRoom.Instance && !FPSRoom.Instance.MapLoading && !AssignmentMatch.IsFinished)
            {
                Lab7Stats.RecordDeath(photonView.Owner);
                photonView.RPC(nameof(ApplyHealth), RpcTarget.All, 0);
            }
        }

        [PunRPC] void ApplyHealth(int health, PhotonMessageInfo info)
        {
            if (!info.Sender.IsMasterClient || health < 0 || health > 100 || Dead || AssignmentMatch.IsFinished) return;
            SetHealth(health);
        }

        void SetHealth(int health)
        {
            if (Dead) return;
            Health = health;
            if (IsLocal && FPSRoom.Instance != null && FPSRoom.Instance.LocalPlayer == this)
            {
                FPSRoom.Instance.RefreshLocalHealth();
                FPSRoom.Instance.hud.ShowDamage();
            }
            if (Dead)
            {
                if (!rb.isKinematic) rb.linearVelocity=Vector3.zero;
                rb.isKinematic=true;
                if (IsLocal) FPSRoom.Instance.LocalDied(this);
                body.SetActive(false);
                thirdPersonWeapon.SetActive(false);
                nicknameLabel.gameObject.SetActive(false);
                GetComponent<CapsuleCollider>().enabled = false;
            }
        }

        [PunRPC] void ShotEffects(Vector3[] points, Vector3[] normals, byte[] flags, int[] targets,
            int weaponIndex, int shot, double sentAt, Vector3 origin, PhotonMessageInfo info)
        {
            if (info.Sender != photonView.Owner || AssignmentMatch.IsFinished || FPSRoom.Instance.MapLoading
                || shot <= acceptedShotId || !ValidEventTime(sentAt) || !Finite(origin)
                || Vector3.Distance(origin, transform.position + Vector3.up * 1.664f) > 4) return;
            if (weaponIndex<0 || weaponIndex>=WeaponCount || points==null || normals==null || flags==null ||
                points.Length>32 || normals.Length!=points.Length || flags.Length!=points.Length
                || targets==null || targets.Length!=points.Length) return;
            EnsureWeapons();
            var spec = runtimeSpecs[weaponIndex];
            if (points.Length != (spec.projectile ? 0 : spec.pelletsCount)) return;
            for (int i=0;i<points.Length;i++)
                if (!Finite(points[i]) || !Finite(normals[i]) || Vector3.Distance(origin,points[i])>101 || flags[i]>3) return;
            // Validate the sender's event time, not packet arrival gaps (which compress under jitter).
            if (!authoritativeGuns[weaponIndex].Fire(sentAt)) return;
            acceptedShotId=shot; acceptedWeapon=weaponIndex; acceptedTargets=(int[])targets.Clone();
            for (int i=0;i<points.Length;i++) Lab7Stats.RecordShot(photonView.Owner);
            int soundIndex = runtimeSpecs[weaponIndex].shootSFXIndex;
            var source=gunShootSource ? gunShootSource : audioSource;
            source.clip=soundIndex>=0 && allGunShootSFX.Length>soundIndex ? allGunShootSFX[soundIndex] : shotSound;
            source.pitch=Random.Range(.7f,1.2f); source.volume=Random.Range(.2f,.35f); source.Play();
            ShotSoundCount++;
            LastPelletCount=points.Length;
            PelletEffectCount+=points.Length;
            bool hitTarget=false;
            for (int i=0;i<points.Length;i++)
            {
                bool target=(flags[i]&2)!=0;
                hitTarget|=target;
                CreatePelletEffect(points[i],normals[i],(flags[i]&1)!=0,target);
            }
            if (IsLocal && hitTarget && FPSRoom.Instance!=null) FPSRoom.Instance.hud.ShowHit();
        }

        void CreatePelletEffect(Vector3 point, Vector3 normal, bool impact, bool hitTarget)
        {
            var beam = new GameObject("ShotTrace").AddComponent<LineRenderer>();
            beam.sharedMaterial = effectMaterial;
            beam.positionCount = 2;
            beam.startWidth = .025f;
            beam.endWidth = .008f;
            beam.SetPosition(0, IsLocal ? weapon.position + weapon.forward * .55f : transform.position + Vector3.up * 1.45f + transform.forward * .5f);
            beam.SetPosition(1, point);
            Destroy(beam.gameObject, .06f);
            if (!impact) return;
            var particles = new GameObject("HitParticles").AddComponent<ParticleSystem>();
            particles.transform.position = point + normal * .02f;
            particles.transform.rotation = Quaternion.LookRotation(normal);
            particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = false; main.duration = .2f; main.startLifetime = .3f;
            main.startSpeed = 2f; main.startSize = .07f; main.gravityModifier = .7f;
            main.startColor = hitTarget ? new Color(.2f, .9f, 1f) : new Color(1f, .7f, .2f);
            var emission = particles.emission; emission.rateOverTime = 0;
            particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = effectMaterial;
            particles.Emit(12);
            Destroy(particles.gameObject, 1);
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            if (IsLocal) photonView.RPC(nameof(InitializeHealth), newPlayer, Health, healthVersion);
        }

        [PunRPC] void InitializeHealth(int health, int version, PhotonMessageInfo info)
        {
            if (info.Sender != photonView.Owner || IsLocal || version < healthVersion || health < 0 || health > 100) return;
            healthVersion = version;
            SetHealth(health);
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            EnsureWeapons();
            if (stream.IsWriting)
            {
                stream.SendNext(pitch);
                stream.SendNext(SelectedWeapon); stream.SendNext(weaponRevision);
                foreach (var gun in guns) { stream.SendNext(gun.Ammo); stream.SendNext(gun.Reserve); stream.SendNext(gun.ReloadUntil); }
            }
            else
            {
                remotePitch = (float)stream.ReceiveNext();
                body.SetActive(!Dead);
                thirdPersonWeapon.SetActive(!Dead);
                nicknameLabel.gameObject.SetActive(!Dead);
                GetComponent<CapsuleCollider>().enabled = !Dead;
                int selected=(int)stream.ReceiveNext(); int revision=(int)stream.ReceiveNext();
                if (selected>=0 && selected<WeaponCount && revision>=weaponRevision) { weaponRevision=revision; SelectedWeapon=selected; ApplyWeaponVisuals(); }
                foreach(var gun in guns) gun.Restore((int)stream.ReceiveNext(), (int)stream.ReceiveNext(), (double)stream.ReceiveNext());
            }
        }
    }
}
