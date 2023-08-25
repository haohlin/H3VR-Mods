using UnityEngine;
using HarmonyLib;
using FistVR;
using System.Collections.Generic;
using System;
using UnityEngine.AI;

namespace BetterSosigSpawner
{
    [HarmonyPatch(typeof(SosigSpawner), "PageUpdate_SpawnSosig")]
    public static class Harmony_PageUpdate_SpawnSosig
    {
        [HarmonyPrefix]
        public static bool Prefix(SosigSpawner __instance)
        {
            Vector3 zero = Vector3.zero;
            Physics.Raycast(__instance.Muzzle.position, __instance.Muzzle.forward, out __instance.m_hit, __instance.Range_PlacementBeam, __instance.LM_PlacementBeam, QueryTriggerInteraction.Ignore);
            __instance.PlacementBeam1.gameObject.SetActive(true);
            __instance.PlacementBeam2.gameObject.SetActive(false);
            __instance.PlacementBeam1.localScale = new Vector3(0.005f, 0.005f, __instance.m_hit.distance);
            __instance.PlacementReticle.gameObject.SetActive(true);
            __instance.PlacementReticle_Valid.SetActive(true);
            __instance.PlacementReticle_Invalid.SetActive(false);
            __instance.m_canSpawn_Sosig = true;
            __instance.m_sosigSpawn_Point = __instance.m_hit.point;
            __instance.PlacementReticle.position = __instance.m_hit.point + Vector3.up * 0.01f;
            return false;
        }
    }

    [HarmonyPatch(typeof(SosigSpawner), "UpdateInteraction_SpawnSosig")]
    public static class Harmony_UpdateInteraction_SpawnSosig
    {
        [HarmonyPrefix]
        public static bool Prefix(SosigSpawner __instance, FVRViveHand hand)
        {
            Physics.Raycast(__instance.Muzzle.position, __instance.Muzzle.forward, out __instance.m_hit, 3000f, __instance.LM_PlacementBeam, QueryTriggerInteraction.Ignore);
            __instance.PlacementReticle.gameObject.SetActive(true);
            __instance.PlacementReticle_Valid.SetActive(true);
            __instance.PlacementReticle_Invalid.SetActive(false);
            __instance.PlacementBeam1.gameObject.SetActive(true);
            __instance.PlacementBeam2.gameObject.SetActive(false);
            __instance.PlacementBeam1.localScale = new Vector3(0.005f, 0.005f, __instance.m_hit.distance);
            __instance.m_canSpawn_Sosig = true;
            __instance.m_sosigSpawn_Point = __instance.m_hit.point;
            __instance.PlacementReticle.position = __instance.m_hit.point + Vector3.up * 0.01f;
            Vector3 position = __instance.gameObject.transform.position;
            if (__instance.m_hasTriggeredUpSinceBegin && hand.Input.TriggerDown)
            {
                if (__instance.m_canSpawn_Sosig)
                {
                    Vector3 a = __instance.m_sosigSpawn_Point - position;
                    a.y = 0f;
                    SM.PlayGenericSound(__instance.AudEvent_Spawn, position);
                    if (__instance.SpawnerGroups[__instance.m_spawn_group].IsFurniture)
                    {
                        UnityEngine.Object.Instantiate<GameObject>(__instance.SpawnerGroups[__instance.m_spawn_group].Furnitures[__instance.m_spawn_template], __instance.m_sosigSpawn_Point + Vector3.up * 0.5f, Quaternion.LookRotation(-a, Vector3.up));
                        return false;
                    }
                    SosigEnemyTemplate template = __instance.SpawnerGroups[__instance.m_spawn_group].Templates[__instance.m_spawn_template];
                    __instance.SpawnSosigWithTemplate(template, __instance.m_sosigSpawn_Point, -a);
                    return false;
                }
                else
                {
                    SM.PlayGenericSound(__instance.AudEvent_Fail, position);
                }
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(Sosig), "Speak_Pain")]
    public static class Harmony_Sosig_Speak_Pain
    {
        [HarmonyPrefix]
        public static bool Prefix(Sosig __instance, List<AudioClip> clips)
        {
            bool flag = false;
            if (__instance.Speech.ForceDeathSpeech && (clips == __instance.Speech.OnDeath || clips == __instance.Speech.OnDeathAlt))
            {
                flag = true;
            }
            if (__instance.BodyState == Sosig.SosigBodyState.Dead && !flag)
            {
                return false;
            }
            if (clips.Count <= 0)
            {
                return false;
            }
            if (!__instance.CanSpeakPain() && !flag)
            {
                return false;
            }
            if (flag)
            {
                __instance.KillSpeech();
            }
            AudioClip audioClip = clips[UnityEngine.Random.Range(0, clips.Count)];
            __instance.m_tickDownToPainSpeechAvailability = audioClip.length + UnityEngine.Random.Range(1.1f, 1.2f);
            Vector3 position = __instance.gameObject.transform.position;
            bool flag2 = true;
            if (__instance.Links[0] != null)
            {
                position = __instance.Links[0].transform.position;
                flag2 = false;
            }
            float num = 1f;
            if (__instance.IsFrozen)
            {
                num = 0.8f;
            }
            if (__instance.IsSpeedUp)
            {
                num = 1.8f;
            }
            if (GM.CurrentAIManager != null)
            {
                if (flag)
                {
                    __instance.m_speakingSource = GM.CurrentAIManager.Speak(audioClip, __instance.Speech.BaseVolume, __instance.Speech.BasePitch * num, position, AIManager.SpeakType.death);
                }
                else
                {
                    __instance.m_speakingSource = GM.CurrentAIManager.Speak(audioClip, __instance.Speech.BaseVolume, __instance.Speech.BasePitch * num, position, AIManager.SpeakType.pain);
                }
            }
            if (__instance.m_speakingSource != null && !flag2)
            {
                __instance.m_speakingSource.FollowThisTransform(__instance.Links[0].transform);
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(Sosig), "Speak_State")]
    public static class Harmony_Sosig_Speak_State
    {
        [HarmonyPrefix]
        public static bool Prefix(Sosig __instance, List<AudioClip> clips)
        {
            if (__instance.BodyState == Sosig.SosigBodyState.Dead)
            {
                return false;
            }
            if (clips.Count <= 0)
            {
                return false;
            }
            AudioClip audioClip = clips[UnityEngine.Random.Range(0, clips.Count)];
            __instance.m_tickDownToNextStateSpeech = audioClip.length + __instance.GetSpeakDelay();
            Vector3 position = __instance.gameObject.transform.position;
            bool flag = true;
            if (__instance.Links[0] != null)
            {
                position = __instance.Links[0].transform.position;
                flag = false;
            }
            float num = 1f;
            if (__instance.IsFrozen)
            {
                num = 0.8f;
            }
            if (__instance.IsSpeedUp)
            {
                num = 1.8f;
            }
            if (GM.CurrentAIManager != null)
            {
                __instance.m_speakingSource = GM.CurrentAIManager.Speak(audioClip, __instance.Speech.BaseVolume, __instance.Speech.BasePitch * num, position, AIManager.SpeakType.chat);
            }
            if (__instance.m_speakingSource != null && !flag)
            {
                __instance.m_speakingSource.FollowThisTransform(__instance.Links[0].transform);
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(Sosig), "Init")]
    public static class Harmony_Sosig_Init
    {
        [HarmonyPrefix]
        public static bool Prefix(Sosig __instance)
        {
            for (int i = 0; i < __instance.Links.Count; i++)
            {
                __instance.IgnoreRBs.Add(__instance.Links[i].R);
            }
            for (int j = 0; j < __instance.Links.Count; j++)
            {
                if (__instance.Links[j].J != null)
                {
                    __instance.m_joints.Add(__instance.Links[j].J);
                }
            }
            if (__instance.E != null)
            {
                __instance.E.AIEventReceiveEvent += __instance.EventReceive;
                __instance.E.AIReceiveSuppressionEvent += __instance.SuppresionEvent;
            }
            __instance.Agent.Warp(__instance.gameObject.transform.position);
            __instance.Agent.enabled = true;
            __instance.m_cachedPath = new NavMeshPath();
            if (__instance.Priority != null && !__instance.m_hasConfiguredPriority)
            {
                __instance.m_hasPriority = true;
                __instance.Priority.Init(__instance.E, 5, 2f, 1.5f);
            }
            __instance.InitHands();
            __instance.Inventory.Init();
            if (GM.CurrentAIManager != null && GM.CurrentAIManager.HasCPM)
            {
                __instance.CoverSearchRange = GM.CurrentAIManager.CPM.DefaultSearchRange;
            }
            __instance.m_targetPose = __instance.Pose_Standing;
            __instance.m_targetLocalPos = __instance.Pose_Standing.localPosition;
            __instance.m_targetLocalRot = __instance.Pose_Standing.localRotation;
            __instance.m_poseLocalEulers_Standing = __instance.Pose_Standing.localEulerAngles;
            __instance.m_poseLocalEulers_Crouching = __instance.Pose_Crouching.localEulerAngles;
            __instance.m_poseLocalEulers_Prone = __instance.Pose_Prone.localEulerAngles;
            __instance.UpdateJoints(1f);
            return false;
        }
    }

    [HarmonyPatch(typeof(Sosig), "SosigDies")]
    public static class Harmony_Sosig_SosigDies
    {
        [HarmonyPrefix]
        public static bool Prefix(Sosig __instance, Damage.DamageClass damClass, Sosig.SosigDeathType deathType)
        {
            if (__instance.BodyState == Sosig.SosigBodyState.Dead)
            {
                return false;
            }
            __instance.RemoveSelfFromPathWiths();
            __instance.DeActivateAllBuffSystems();
            if (damClass != Damage.DamageClass.Abstract)
            {
                __instance.m_diedFromClass = damClass;
            }
            __instance.m_diedFromType = deathType;
            if (!__instance.m_linksDestroyed[0])
            {
                __instance.Speak_Pain(__instance.Speech.OnDeath);
            }
            else
            {
                __instance.KillSpeech();
                if (__instance.Speech.UseAltDeathOnHeadExplode)
                {
                    __instance.Speak_Pain(__instance.Speech.OnDeathAlt);
                }
            }
            __instance.SetBodyState(Sosig.SosigBodyState.Dead);
            __instance.CurrentOrder = Sosig.SosigOrder.Disabled;
            __instance.FallbackOrder = Sosig.SosigOrder.Disabled;
            __instance.SetHandObjectUsage(Sosig.SosigObjectUsageFocus.EmptyHands);
            __instance.SetMovementState(Sosig.SosigMovementState.Idle);
            if (GM.CurrentAIManager != null)
            {
                if (GM.CurrentAIManager.UsesSosigCorpsePerceptionSystem)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (!__instance.m_linksDestroyed[i] && __instance.Links[i] != null)
                        {
                            GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(__instance.CorpseEntityPrefab, __instance.Links[i].transform.position, __instance.Links[i].transform.rotation);
                            gameObject.transform.SetParent(__instance.Links[i].transform);
                            gameObject.GetComponent<AIEntity>().IFFCode = __instance.E.IFFCode;
                        }
                    }
                }
            }
            __instance.E.IFFCode = -3;
            for (int j = 0; j < __instance.Hands.Count; j++)
            {
                __instance.Hands[j].DropHeldObject();
            }
            __instance.Inventory.DropAllObjects();
            for (int k = 0; k < __instance.Links.Count; k++)
            {
                if (__instance.Links[k] != null && !__instance.m_jointsSevered[k])
                {
                    __instance.Links[k].R.AddForce(UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(1f, 5f), ForceMode.VelocityChange);
                    __instance.Links[k].O.DistantGrabbable = true;
                }
            }
            GM.CurrentSceneSettings.OnSosigKill(__instance);
            return false;
        }
    }
    //private Sosig SpawnEnemySosig(SosigEnemyTemplate template, Vector3 position, Vector3 forward, int IFF)
    //{
    //    FVRObject fvrobject = template.SosigPrefabs[UnityEngine.Random.Range(0, template.SosigPrefabs.Count)];
    //    SosigConfigTemplate t = template.ConfigTemplates[UnityEngine.Random.Range(0, template.ConfigTemplates.Count)];
    //    SosigOutfitConfig w = template.OutfitConfig[UnityEngine.Random.Range(0, template.OutfitConfig.Count)];
    //    Sosig sosig = this.SpawnSosigAndConfigureSosig(fvrobject.GetGameObject(), position, Quaternion.LookRotation(forward, Vector3.up), t, w);
    //    sosig.InitHands();
    //    sosig.Inventory.Init();
    //    sosig.Inventory.FillAllAmmo();
    //    sosig.SetIFF(IFF);
    //    if (template.WeaponOptions.Count > 0)
    //    {
    //        SosigWeapon sosigWeapon = this.SpawnWeapon(template.WeaponOptions);
    //        sosigWeapon.SetAutoDestroy(true);
    //        sosigWeapon.SetAmmoClamping(true);
    //        sosigWeapon.O.SpawnLockable = false;
    //        sosig.ForceEquip(sosigWeapon);
    //    }
    //    bool flag = false;
    //    float num = UnityEngine.Random.Range(0f, 1f);
    //    if (num <= template.SecondaryChance)
    //    {
    //        flag = true;
    //    }
    //    if (template.WeaponOptions_Secondary.Count > 0 && flag)
    //    {
    //        SosigWeapon sosigWeapon2 = this.SpawnWeapon(template.WeaponOptions_Secondary);
    //        sosigWeapon2.SetAutoDestroy(true);
    //        sosigWeapon2.SetAmmoClamping(true);
    //        sosigWeapon2.O.SpawnLockable = false;
    //        sosig.ForceEquip(sosigWeapon2);
    //    }
    //    bool flag2 = false;
    //    num = UnityEngine.Random.Range(0f, 1f);
    //    if (num <= template.TertiaryChance)
    //    {
    //        flag2 = true;
    //    }
    //    if (template.WeaponOptions_Tertiary.Count > 0 && flag2)
    //    {
    //        SosigWeapon sosigWeapon3 = this.SpawnWeapon(template.WeaponOptions_Tertiary);
    //        sosigWeapon3.SetAutoDestroy(true);
    //        sosigWeapon3.SetAmmoClamping(true);
    //        sosigWeapon3.O.SpawnLockable = false;
    //        sosig.ForceEquip(sosigWeapon3);
    //    }
    //    if (this.IsPatrolZone)
    //    {
    //        sosig.CurrentOrder = Sosig.SosigOrder.Assault;
    //        sosig.FallbackOrder = Sosig.SosigOrder.Assault;
    //        sosig.CommandAssaultPoint(this.PatrolPoints[0].position); // should set patrol point to current
    //    }
    //    else
    //    {
    //        sosig.CurrentOrder = this.DefaultOrder;
    //        sosig.FallbackOrder = this.DefaultOrder;
    //        if (this.DefaultOrder == Sosig.SosigOrder.GuardPoint)
    //        {
    //            sosig.SetGuardInvestigateDistanceThreshold(40f);
    //            sosig.SetDominantGuardDirection(forward);
    //        }
    //    }
    //    return sosig;
    //}

    //private void Check() // Winterwastlandenemyspawn
    //{
    //    this.CheckDespawn();
    //    Vector3 position = GM.CurrentPlayerBody.Head.position;
    //    bool flag = false;
    //    if (this.IsPlayerInAnyVolumes(position))
    //    {
    //        if (!this.m_wasIn)
    //        {
    //            this.m_wasIn = true;
    //            if (this.DoesSpawnOnEntry && this.m_onEntryCooldownTick <= 0f)
    //            {
    //                flag = true;
    //                this.m_onEntryCooldownTick = UnityEngine.Random.Range(this.OnEntryCooldown.x, this.OnEntryCooldown.y);
    //            }
    //        }
    //    }
    //    else
    //    {
    //        this.m_wasIn = false;
    //    }
    //    if (this.m_timeUntilSpawnCheck > 0f && !flag)
    //    {
    //        return;
    //    }
    //    this.m_timeUntilSpawnCheck = UnityEngine.Random.Range(this.RefireTickRangeAfterSpawnFailure.x, this.RefireTickRangeAfterSpawnFailure.y);
    //    if (this.m_spawnedSosigs.Count >= this.MaxCanBeAlive)
    //    {
    //        return;
    //    }
    //    if (this.UsesMaxTotalSpawnedCount && this.m_spawnedSofar >= this.MaxToSpawnEver)
    //    {
    //        return;
    //    }
    //    if (!this.IsPlayerInAnyVolumes(position))
    //    {
    //        return;
    //    }
    //    Vector3 forward = GM.CurrentPlayerBody.Head.forward;
    //    bool flag2 = false;
    //    WinterEnemySpawnZone.SpawnGroup group = this.Group;
    //    int num = UnityEngine.Random.Range(group.MinSpawnedInGroup, group.MaxSpawnedInGroup + 1);
    //    List<Transform> list = new List<Transform>();
    //    if (this.IsPatrolZone)
    //    {
    //        for (int i = 0; i < this.PatrolSpawnPoints.Count; i++)
    //        {
    //            list.Add(this.PatrolSpawnPoints[i]);
    //        }
    //    }
    //    else
    //    {
    //        for (int j = 0; j < this.SpawnPoints.Count; j++)
    //        {
    //            list.Add(this.SpawnPoints[j]);
    //        }
    //    }
    //    if (list.Count > 0)
    //    {
    //        list.Shuffle<Transform>();
    //    }
    //    for (int k = 0; k < num; k++) // should not spawn all num enemys, but max_allowed_alive - num
    //    {
    //        if (list.Count < 1)
    //        {
    //            break;
    //        }
    //        if (this.UsesMaxTotalSpawnedCount && this.m_spawnedSofar >= this.MaxToSpawnEver)
    //        {
    //            break;
    //        }
    //        bool flag3 = false;
    //        int index = 0;
    //        for (int l = list.Count - 1; l >= 0; l--)
    //        {
    //            bool flag4 = this.IsUsefulPoint(list[l].position, position, forward);
    //            if (flag4)
    //            {
    //                flag3 = true;
    //                index = l;
    //                break;
    //            }
    //        }
    //        if (flag3)
    //        {
    //            Transform point = list[index];
    //            list.RemoveAt(index);
    //            this.SpawnEnemy(group.GetTemplate(), point, group.IFF);
    //            flag2 = true;
    //            this.m_spawnedSofar++;
    //        }
    //    }
    //    list.Clear();
    //    if (flag2)
    //    {
    //        this.m_timeUntilSpawnCheck = UnityEngine.Random.Range(this.RefireTickRangeAfterSpawnSuccess.x, this.RefireTickRangeAfterSpawnSuccess.y);
    //    }
    //}
}
