using UnityEngine;
using HarmonyLib;
using FistVR;
using System.Linq;
using System;

namespace BetterGasTank
{

    [HarmonyPatch(typeof(Brut_GasCuboid), "GenerateGout")]
    public static class Harmony_GenerateGout
    {
        [HarmonyPrefix]
        public static bool Prefix(Brut_GasCuboid __instance, Vector3 point, Vector3 normal)
        {
            if (__instance.hasGeneratedGoutYet)
            {
                return false;
            }
            if (__instance.m_isDestroyed)
            {
                return false;
            }
            if (__instance.m_fuel <= 0f)
            {
                return false;
            }
            __instance.hasGeneratedGoutYet = true;
            SM.PlayCoreSound(FVRPooledAudioType.Generic, __instance.AudEvent_GoutStart, __instance.gameObject.transform.position);
            if (!__instance.AudSource_GoutLoop.isPlaying)
            {
                __instance.AudSource_GoutLoop.Play();
            }
            if (!__instance.m_hasGoneNoFric)
            {
                __instance.m_hasGoneNoFric = true;
                __instance.NoFricCollider.material = __instance.NoFricMaterial;
            }

            //__instance.RB.AddForce(Vector3.up, ForceMode.VelocityChange);
            //__instance.RB.AddTorque(UnityEngine.Random.onUnitSphere, ForceMode.VelocityChange);
            GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(__instance.FireGoutPrefab, point, Quaternion.LookRotation(normal, UnityEngine.Random.onUnitSphere));
            gameObject.transform.SetParent(__instance.gameObject.transform);
            Brut_GasCuboidGout component = gameObject.GetComponent<Brut_GasCuboidGout>();
            __instance.m_gouts.Add(component);
            component.TurnOn();
            return false;
        }
    }

    [HarmonyPatch(typeof(Brut_GasCuboid), "FixedUpdate")]
    public static class Harmony_FixedUpdate
    {
        [HarmonyPrefix]
        public static bool Prefix(Brut_GasCuboid __instance)
        {
            if (__instance.RB == null)
            {
                return false;
            }

            if (__instance.m_gouts.Any())
            {
                RaycastHit hit;
                Physics.Raycast(__instance.gameObject.transform.position, __instance.gameObject.transform.up, out hit, 100f, __instance.m_gouts[0].Burning_LM);
                //Console.WriteLine("up : {0}, hit distance: {1}", __instance.gameObject.transform.up, hit.distance);
                Physics.Raycast(__instance.gameObject.transform.position, __instance.gameObject.transform.forward, out hit, 100f, __instance.m_gouts[0].Burning_LM);
                //Console.WriteLine("forward : {0}, hit distance: {1}", __instance.gameObject.transform.forward, hit.distance);
                Physics.Raycast(__instance.gameObject.transform.position, __instance.gameObject.transform.right, out hit, 100f, __instance.m_gouts[0].Burning_LM);
                //Console.WriteLine("right : {0}, hit distance: {1}", __instance.gameObject.transform.right, hit.distance);

                float max_dist;
                float air_drag_ofst;
                float area;
                if (__instance.gameObject.name == "Brut_GasCuboid_Cryo_Large(Clone)" || __instance.gameObject.name == "Brut_GasCuboid_Cryo_Large_attach(Clone)" || __instance.gameObject.name == "Brut_GasCuboid_Cryo_Large_mount(Clone)")
                {
                    air_drag_ofst = 0.005f;
                    area = 0.1f;
                    max_dist = 1f;
                }
                else
                {
                    air_drag_ofst = 0.0002f;
                    area = 0.05f;
                    max_dist = 0.7f;
                }

                // Air resistance
                var p = 1.225f;
                var cd = .47f;
                var v = __instance.RB.velocity.magnitude / 5;
                var direction = -__instance.RB.velocity.normalized;
                var forceAmount = (p * v * v * cd * area) / 2;
                Vector3 fly_direction = -__instance.gameObject.transform.up;
                //var forcePoint = __instance.gameObject.transform.position + __instance.gameObject.transform.up * air_drag_ofst;
                var forcePoint = __instance.RB.worldCenterOfMass + __instance.gameObject.transform.up * air_drag_ofst;
                if (__instance.gameObject.name == "Brut_GasCuboid_Cryo_Large_attach(Clone)")
                {
                    //__instance.RB.centerOfMass = new Vector3(0f, 0f, __instance.RB.centerOfMass.z);
                    fly_direction = __instance.gameObject.transform.forward;
                    forcePoint = __instance.RB.worldCenterOfMass - __instance.gameObject.transform.forward * air_drag_ofst;
                }
                //__instance.RB.AddForceAtPosition(direction * forceAmount, forcePoint);
                Console.WriteLine("Applying force amount: {0} at: {1}, RB worldCenterOfMass: {2}", forceAmount, forcePoint, __instance.RB.worldCenterOfMass);

                if (__instance.RB.velocity.magnitude > 10 && Physics.Raycast(__instance.gameObject.transform.position, fly_direction, out hit, max_dist, __instance.m_gouts[0].Burning_LM))
                {
                    //Console.WriteLine("exploding at velocity: {0}, hit distance: {1}", __instance.RB.velocity.magnitude, hit.distance);
                    __instance.Explode(__instance.gameObject.transform.position, -__instance.gameObject.transform.up, false);
                }
            }


            if (__instance.m_fuel > 0f)
            {
                for (int i = 0; i < __instance.m_gouts.Count; i++)
                {
                    //Vector3 pos_i = __instance.m_gouts[i].transform.localPosition;
                    //__instance.m_gouts[i].transform.localPosition= new Vector3(0f, pos_i.y, 0f);
                    __instance.RB.AddForceAtPosition(-__instance.m_gouts[i].transform.forward * __instance.GoutForce, __instance.m_gouts[i].transform.position, ForceMode.Acceleration);
                    __instance.m_gouts[i].Burn();
                }
            }
            return false;
        }
    }


    [HarmonyPatch(typeof(FVRFireArmAttachment), "AttachToMount")]
    public static class Harmony_AttachToMount
    {
        [HarmonyPrefix]
        public static bool Prefix(FVRFireArmAttachment __instance, FVRFireArmAttachmentMount m, bool playSound)
        {
            __instance.curMount = m;
            if (__instance.gameObject.name == "Brut_GasCuboid_Cryo_Large_attach(Clone)")
            {
                Console.WriteLine("Generating FixedJoint!");
                __instance.gameObject.AddComponent<FixedJoint>();
                __instance.gameObject.GetComponent<FixedJoint>().connectedBody = __instance.curMount.Parent.RootRigidbody;
            }
            else
            {
                __instance.StoreAndDestroyRigidbody();
            }
            if (__instance.curMount.GetRootMount().ParentToThis)
            {
                __instance.SetParentage(__instance.curMount.GetRootMount().transform);
            }
            else
            {
                __instance.SetParentage(__instance.curMount.MyObject.transform);
            }
            if (__instance.IsBiDirectional)
            {
                if (Vector3.Dot(__instance.gameObject.transform.forward, __instance.curMount.transform.forward) >= 0f)
                {
                    __instance.gameObject.transform.rotation = __instance.curMount.transform.rotation;
                }
                else
                {
                    __instance.gameObject.transform.rotation = Quaternion.LookRotation(-__instance.curMount.transform.forward, __instance.curMount.transform.up);
                }
            }
            else
            {
                __instance.gameObject.transform.rotation = __instance.curMount.transform.rotation;
            }
            __instance.gameObject.transform.position = __instance.GetClosestValidPoint(__instance.curMount.Point_Front.position, __instance.curMount.Point_Rear.position, __instance.gameObject.transform.position);
            if (__instance.curMount.Parent != null)
            {
                __instance.curMount.Parent.RegisterAttachment(__instance);
            }
            __instance.curMount.RegisterAttachment(__instance);
            if (__instance.curMount.Parent != null && __instance.curMount.Parent.QuickbeltSlot != null)
            {
                __instance.SetAllCollidersToLayer(false, "NoCol");
            }
            else
            {
                __instance.SetAllCollidersToLayer(false, "Default");
            }
            if (__instance.AttachmentInterface != null)
            {
                __instance.AttachmentInterface.OnAttach();
                __instance.AttachmentInterface.gameObject.SetActive(true);
            }
            __instance.SetTriggerState(false);
            if (__instance.DisableOnAttached != null)
            {
                __instance.DisableOnAttached.SetActive(false);
            }
            return false;
        }
    }
}
