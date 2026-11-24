using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Photon.Pun;
using UnityEditor;
using UnityEngine;

namespace Lab6.Editor
{
    public static class ReauditLab6Assets
    {
        const string PlayerPath = "Assets/Resources/Lab6Player.prefab";

        [MenuItem("Assignment3/Apply audited Lab6 player settings")]
        public static void ApplyReauditAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            var root = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                ApplyPlayerSettings(root.GetComponent<FPSPlayer>());
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            SelfCheck();
        }

        public static void ApplyPlayerSettings(FPSPlayer player)
        {
            SprintInput(true);
            var root = player.gameObject;
            var controller = root.GetComponent<CharacterController>();
            if (controller) UnityEngine.Object.DestroyImmediate(controller);
            var body = root.GetComponent<Rigidbody>();
            if (!body) body = root.AddComponent<Rigidbody>();
            body.mass = 1; body.useGravity = true; body.isKinematic = false;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            var capsule = root.GetComponent<CapsuleCollider>();
            capsule.height = 2; capsule.radius = .5f; capsule.center = Vector3.up; capsule.enabled = true;
            var feet = root.GetComponent<SphereCollider>();
            if (!feet) feet = root.AddComponent<SphereCollider>();
            // The tutorial's center-origin capsule is translated +1 m to retain this project's foot-origin spawns.
            feet.isTrigger = true; feet.center = new Vector3(0, .347f, 0); feet.radius = .5093457f;
            player.body.transform.localPosition = Vector3.up;
            player.body.transform.localScale = Vector3.one;
            player.viewCamera.transform.localPosition = new Vector3(0, 1.664f, 0);
            player.viewCamera.fieldOfView = 60;
            player.viewCamera.nearClipPlane = .01f;
            if (player.weaponCamera) player.weaponCamera.fieldOfView = 60;
            player.walkSpeed = 4; player.sprintSpeed = 14; player.maxVelocityChange = 10;
            player.airControl = .5f; player.jumpHeight = 5;
            player.swayClamp = .09f; player.swaySmoothing = 3;
            var rigidbodyView = root.GetComponent<PhotonRigidbodyView>();
            if (!rigidbodyView) rigidbodyView = root.AddComponent<PhotonRigidbodyView>();
            rigidbodyView.m_SynchronizeVelocity = false;
            rigidbodyView.m_SynchronizeAngularVelocity = false;
            rigidbodyView.m_TeleportEnabled = true;
            rigidbodyView.m_TeleportIfDistanceGreaterThan = 3;
            var transformView = root.GetComponent<PhotonTransformView>();
            if (!transformView) transformView = root.AddComponent<PhotonTransformView>();
            transformView.m_SynchronizePosition = transformView.m_SynchronizeRotation = true;
            transformView.m_SynchronizeScale = false; transformView.m_UseLocal = true;
            var view = root.GetComponent<PhotonView>();
            view.ObservedComponents = new List<Component> { rigidbodyView, transformView, player };
            view.Synchronization = ViewSynchronization.UnreliableOnChange;
        }

        // E02 06:32: the named Sprint action copies the keyboard Jump axis, bound only to left shift.
        static void SprintInput(bool configure)
        {
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset");
            if (settings.Length == 0) throw new InvalidOperationException("Input Manager settings are missing");
            using var manager = new SerializedObject(settings[0]);
            var axes = manager.FindProperty("m_Axes");
            SerializedProperty sprint = null;
            bool mouseFire = false;
            for (int i = 0; i < axes.arraySize; i++)
            {
                var axis = axes.GetArrayElementAtIndex(i);
                var action = axis.FindPropertyRelative("m_Name").stringValue;
                if (action == "Fire1" && axis.FindPropertyRelative("type").intValue == 0 &&
                    (axis.FindPropertyRelative("positiveButton").stringValue == "mouse 0" || axis.FindPropertyRelative("altPositiveButton").stringValue == "mouse 0"))
                    mouseFire = true;
                if (action != "Sprint") continue;
                if (sprint != null) throw new InvalidOperationException("Duplicate Sprint action");
                sprint = axis;
            }
            if (configure && sprint == null)
            {
                int index = axes.arraySize++;
                sprint = axes.GetArrayElementAtIndex(index);
                sprint.FindPropertyRelative("m_Name").stringValue = "Sprint";
                sprint.FindPropertyRelative("positiveButton").stringValue = "left shift";
                foreach (var name in new[] { "negativeButton", "altPositiveButton", "altNegativeButton", "descriptiveName", "descriptiveNegativeName" })
                    sprint.FindPropertyRelative(name).stringValue = "";
                sprint.FindPropertyRelative("gravity").floatValue = sprint.FindPropertyRelative("sensitivity").floatValue = 1000;
                sprint.FindPropertyRelative("dead").floatValue = .001f;
                sprint.FindPropertyRelative("snap").boolValue = sprint.FindPropertyRelative("invert").boolValue = false;
                foreach (var name in new[] { "type", "axis", "joyNum" }) sprint.FindPropertyRelative(name).intValue = 0;
                manager.ApplyModifiedPropertiesWithoutUndo();
            }
            if (sprint == null || sprint.FindPropertyRelative("positiveButton").stringValue != "left shift"
                || sprint.FindPropertyRelative("altPositiveButton").stringValue != "" || sprint.FindPropertyRelative("type").intValue != 0)
                throw new InvalidOperationException("Expected the tutorial Sprint keyboard action bound to left shift");
            if (!mouseFire) throw new InvalidOperationException("Expected Fire1 to include mouse 0");
        }

        public static void SelfCheck()
        {
            SprintInput(false);
            void Check(bool value, string text) { if (!value) throw new InvalidOperationException("Lab6 reaudit: " + text); }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            var p = prefab.GetComponent<FPSPlayer>();
            var capsule = prefab.GetComponent<CapsuleCollider>();
            var feet = prefab.GetComponent<SphereCollider>();
            var body = prefab.GetComponent<Rigidbody>();
            Check(body && body.mass == 1 && body.useGravity && !body.isKinematic && body.constraints == (RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ), "tutorial Rigidbody mass, gravity and X/Z rotation constraints");
            Check(body.interpolation == RigidbodyInterpolation.None && body.collisionDetectionMode == CollisionDetectionMode.Discrete, "tutorial Rigidbody interpolation and collision detection");
            Check(!prefab.GetComponent<CharacterController>() && capsule.height == 2 && capsule.radius == .5f && capsule.center == Vector3.up, "tutorial capsule with foot-origin translation");
            Check(feet.isTrigger && Mathf.Abs(feet.center.y - .347f) < .00001f && Mathf.Abs(feet.radius - .5093457f) < .00001f, "tutorial ground sphere");
            Check(p.body.transform.localScale == Vector3.one && p.body.transform.localPosition == Vector3.up, "tutorial bean dimensions");
            Check(p.viewCamera.fieldOfView == 60 && Mathf.Abs(p.viewCamera.transform.localPosition.y - 1.664f) < .00001f, "tutorial FOV and camera height");
            Check(!p.weaponCamera || p.weaponCamera.fieldOfView == 60, "matching weapon FOV");
            Check(p.walkSpeed == 4 && p.sprintSpeed == 14 && p.maxVelocityChange == 10 && p.airControl == .5f && p.jumpHeight == 5, "final E16 movement values supersede E02 jump 30");
            var rigidbodyView = prefab.GetComponent<PhotonRigidbodyView>();
            var transformView = prefab.GetComponent<PhotonTransformView>();
            var observed = prefab.GetComponent<PhotonView>().ObservedComponents;
            Check(rigidbodyView && transformView && observed.Count == 3 && observed[0] == rigidbodyView && observed[1] == transformView && observed[2] == p, "tutorial PUN views and supplemental weapon state");
            Check(rigidbodyView.m_TeleportEnabled && rigidbodyView.m_TeleportIfDistanceGreaterThan == 3 && !rigidbodyView.m_SynchronizeVelocity && !rigidbodyView.m_SynchronizeAngularVelocity, "tutorial RigidbodyView options");
            Check(transformView.m_SynchronizePosition && transformView.m_SynchronizeRotation && !transformView.m_SynchronizeScale && transformView.m_UseLocal, "tutorial TransformView options");

            var instance = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                var player = instance.GetComponent<FPSPlayer>();
                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
                void Set(string field, object value) => typeof(FPSPlayer).GetField(field, flags).SetValue(player, value);
                void Reset(bool aiming = false)
                {
                    Set("pitch", 0f); Set("lookYaw", 0f); Set("smoothMouse", Vector2.zero);
                    Set("initialLookRotation", Quaternion.identity); Set("initialBodyRotation", Quaternion.Euler(0, 30, 0));
                    typeof(FPSPlayer).GetProperty(nameof(FPSPlayer.Aiming)).SetValue(player, aiming);
                }
                void Look(Vector2 input) => typeof(FPSPlayer).GetMethod("ApplyMouseLook", flags).Invoke(player, new object[] { input });
                Reset(); Look(Vector2.right);
                Check(Mathf.Abs(Mathf.DeltaAngle(player.transform.localEulerAngles.y, 32)) < .001f, "first mouse delta uses sensitivity 2 and retains spawn yaw");
                Look(Vector2.right);
                Check(Mathf.Abs(Mathf.DeltaAngle(player.transform.localEulerAngles.y, 35.333333f)) < .001f, "second delta uses tutorial smoothing recurrence");
                Reset(); Look(new Vector2(0, 100));
                Check(Quaternion.Angle(player.viewCamera.transform.localRotation, Quaternion.Euler(-90, 0, 0)) < .01f, "upward view clamps at 90 degrees");
                Reset(); Look(new Vector2(0, -100));
                Check(Quaternion.Angle(player.viewCamera.transform.localRotation, Quaternion.Euler(90, 0, 0)) < .01f, "downward view clamps at 90 degrees");
                Reset(true); Look(Vector2.right);
                Check(Mathf.Abs(Mathf.DeltaAngle(player.transform.localEulerAngles.y, 31.1f)) < .001f, "approved aim sensitivity multiplier");
                Check(!player.IsLocal, "health fixture is a remote prefab");
                Set("rb", player.GetComponent<Rigidbody>());
                var setHealth = typeof(FPSPlayer).GetMethod("SetHealth", flags);
                var initializeHealth = typeof(FPSPlayer).GetMethod("InitializeHealth", flags);
                setHealth.Invoke(player, new object[] { 25 }); Set("healthVersion", 2);
                initializeHealth.Invoke(player, new object[] { 75, 1, default(PhotonMessageInfo) });
                Check(player.Health == 25, "late old HP snapshot cannot overwrite a newer confirmation");
                initializeHealth.Invoke(player, new object[] { 50, 3, default(PhotonMessageInfo) });
                Check(player.Health == 50, "newer absolute HP snapshot converges");
                initializeHealth.Invoke(player, new object[] { 101, 4, default(PhotonMessageInfo) });
                Check(player.Health == 50, "out-of-range HP snapshot is rejected");
                setHealth.Invoke(player, new object[] { 0 });
                initializeHealth.Invoke(player, new object[] { 50, 5, default(PhotonMessageInfo) });
                Check(player.Dead, "late HP messages cannot revive a destroyed-avatar lifecycle");
            }
            finally { PrefabUtility.UnloadPrefabContents(instance); }
            File.WriteAllText("Lab6.reaudit-checks.txt", "PASS: named Sprint (left shift) and Fire1 (mouse 0); tutorial FOV, smoothed look, view clamp, body/collider dimensions, camera height, Rigidbody mass/gravity/FreezeXZ/None/Discrete, PUN Transform/Rigidbody views; approved aim multiplier.");
        }
    }
}
