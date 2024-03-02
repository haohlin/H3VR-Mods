using UnityEngine;
using HarmonyLib;
using FistVR;
using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;
using UnityEngine.AI;
using FMOD;
using System.Globalization;

namespace BetterScopeControl
{
    public class AmplifierAdditionalData : FVRFireArmAttachmentInterface
    {
        public float time_tickdown;

        public AmplifierAdditionalData()
        {
            time_tickdown = 1f;
        }
    }

    //public static class AmplifierExtensionClass
    //{
    //    private static readonly ConditionalWeakTable<Amplifier, AmplifierAdditionalData> data = new ConditionalWeakTable<Amplifier, AmplifierAdditionalData>();

    //    public static AmplifierAdditionalData GetAdditionalData(this Amplifier ampObj)
    //    {
    //        return data.GetOrCreateValue(ampObj);
    //    }

    //    public static void AddData(this Amplifier ampObj, AmplifierAdditionalData value)
    //    {
    //        try
    //        {
    //            data.Add(ampObj, value);
    //        }
    //        catch (Exception) { }
    //    }
    //}

    [HarmonyPatch]
    public class Harmony_UpdateInteraction_Amplifier
    {
        public AmplifierAdditionalData additionalData = new AmplifierAdditionalData();

        [HarmonyReversePatch]
        [HarmonyPatch(typeof(FVRInteractiveObject), "UpdateInteraction")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void FVRInteractiveObject_UpdateInteraction(FVRInteractiveObject __instance, FVRViveHand hand)
        {
            __instance.IsHeld = true;
            __instance.m_hand = hand;
            if (!__instance.m_hasTriggeredUpSinceBegin && __instance.m_hand.Input.TriggerFloat < 0.15f)
            {
                __instance.m_hasTriggeredUpSinceBegin = true;
            }
            if (__instance.triggerCooldown > 0f)
            {
                __instance.triggerCooldown -= Time.deltaTime;
            }
        }

        [HarmonyReversePatch]
        [HarmonyPatch(typeof(FVRFireArmAttachmentInterface), "UpdateInteraction")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void FVRFireArmAttachmentInterface_UpdateInteraction(FVRFireArmAttachmentInterface __instance, FVRViveHand hand)
        {
            FVRInteractiveObject_UpdateInteraction(__instance, hand);
            bool flag = false;
            if (hand.IsInStreamlinedMode)
            {
                if (hand.Input.AXButtonDown)
                {
                    flag = true;
                }
            }
            else if (hand.Input.TouchpadDown && hand.Input.TouchpadAxes.magnitude > 0.25f && Vector2.Angle(hand.Input.TouchpadAxes, Vector2.down) <= 45f)
            {
                flag = true;
            }
            if (flag && !__instance.IsLocked && __instance.Attachment != null && __instance.Attachment.curMount != null && !__instance.HasAttachmentsOnIt() && __instance.Attachment.CanDetach())
            {
                __instance.DetachRoutine(hand);
            }
        }

        [HarmonyPatch(typeof(Amplifier), "Awake")]
        [HarmonyPostfix]
        public static void Awake_Postfix(Amplifier __instance)
        {
            AmplifierAdditionalData additionalDataGen = __instance.gameObject.AddComponent(typeof(AmplifierAdditionalData)) as AmplifierAdditionalData;
            Traverse.Create(typeof(Harmony_UpdateInteraction_Amplifier)).Field("additionalData").SetValue(additionalDataGen);
        }

        [HarmonyPatch(typeof(Amplifier), "UpdateInteraction")]
        [HarmonyPrefix]
        public static bool UpdateInteraction_Prefix(Amplifier __instance, FVRViveHand hand)
        {
            //float currentSecond = 100 * float.Parse(DateTime.UtcNow.ToString("ss.ff", CultureInfo.InvariantCulture));
            AmplifierAdditionalData curAdditionalData = Traverse.Create(typeof(Harmony_UpdateInteraction_Amplifier)).Field("additionalData").GetValue() as AmplifierAdditionalData;

            //Console.WriteLine("current time is {0}", currentSecond);
            OpticOptionType curOption = __instance.OptionTypes[__instance.CurSelectedOptionIndex];
            Vector2 touchpadAxes = hand.Input.TouchpadAxes;
            if (!__instance.DoesFlip)
            {
                if (hand.IsInStreamlinedMode)
                {
                    if (hand.Input.BYButtonDown)
                    {
                        __instance.isUIActive = !__instance.isUIActive;
                        __instance.UI.gameObject.SetActive(__instance.isUIActive);
                    }
                }
                //else if (hand.Input.TouchpadPressed && __instance.OptionTypes[__instance.CurSelectedOptionIndex] == OpticOptionType.ElevationTweak && touchpadAxes.magnitude > 0.25f)
                //{
                //    if (Vector2.Angle(touchpadAxes, Vector2.left) <= 45f)
                //    {
                //        __instance.SetCurSettingDown();
                //        __instance.UI.UpdateUI(__instance);
                //    }
                //    else if (Vector2.Angle(touchpadAxes, Vector2.right) <= 45f)
                //    {
                //        __instance.SetCurSettingUp(false);
                //        __instance.UI.UpdateUI(__instance);
                //    }
                //    else if (hand.Input.TouchpadDown && Vector2.Angle(touchpadAxes, Vector2.up) <= 45f)
                //    {
                //        __instance.GoToNextSetting();
                //        __instance.UI.UpdateUI(__instance);
                //    }
                //}
                else if (hand.Input.TouchpadDown && touchpadAxes.magnitude > 0.25f)
                {
                    if (Vector2.Angle(touchpadAxes, Vector2.left) <= 45f)
                    {
                        __instance.SetCurSettingDown();
                        __instance.UI.UpdateUI(__instance);
                    }
                    else if (Vector2.Angle(touchpadAxes, Vector2.right) <= 45f)
                    {
                        __instance.SetCurSettingUp(false);
                        __instance.UI.UpdateUI(__instance);
                    }
                    else if (Vector2.Angle(touchpadAxes, Vector2.up) <= 45f)
                    {
                        __instance.GoToNextSetting();
                        __instance.UI.UpdateUI(__instance);
                    }
                }
                //else if (currentSecond % 20 == 0 && hand.Input.TouchpadPressed && curOption == OpticOptionType.ElevationTweak && touchpadAxes.magnitude > 0.25f)
                //{
                //    if (Vector2.Angle(touchpadAxes, Vector2.left) <= 45f)
                //    {
                //        __instance.SetCurSettingDown();
                //        __instance.UI.UpdateUI(__instance);
                //    }
                //    else if (Vector2.Angle(touchpadAxes, Vector2.right) <= 45f)
                //    {
                //        __instance.SetCurSettingUp(false);
                //        __instance.UI.UpdateUI(__instance);
                //    }
                //}
                else if (hand.Input.TouchpadPressed && touchpadAxes.magnitude > 0.25f)
                {
                    Console.WriteLine("current time_tickdown is {0}", curAdditionalData.time_tickdown);
                    if (curAdditionalData.time_tickdown > 0)
                    {
                        curAdditionalData.time_tickdown -= Time.deltaTime;
                        Traverse.Create(typeof(Harmony_UpdateInteraction_Amplifier)).Field("additionalData").SetValue(curAdditionalData);
                    }
                    else
                    {
                        if (Vector2.Angle(touchpadAxes, Vector2.left) <= 45f)
                        {
                            __instance.SetCurSettingDown();
                            __instance.UI.UpdateUI(__instance);
                        }
                        else if (Vector2.Angle(touchpadAxes, Vector2.right) <= 45f)
                        {
                            __instance.SetCurSettingUp(false);
                            __instance.UI.UpdateUI(__instance);
                        }
                        else if (Vector2.Angle(touchpadAxes, Vector2.up) <= 45f)
                        {
                            __instance.GoToNextSetting();
                            __instance.UI.UpdateUI(__instance);
                        }
                    }
                }
                else if (hand.Input.TouchpadUp)
                {
                    Traverse.Create(typeof(Harmony_UpdateInteraction_Amplifier)).Field("additionalData").SetValue(new AmplifierAdditionalData());
                }
            }
            else if (hand.IsInStreamlinedMode)
            {
                if (hand.Input.BYButtonDown)
                {
                    __instance.m_flippedUp = !__instance.m_flippedUp;
                    SM.PlayCoreSound(FVRPooledAudioType.GenericClose, __instance.AudEvent_Flip, __instance.gameObject.transform.position);
                }
            }
            else if (hand.Input.TouchpadDown && touchpadAxes.magnitude > 0.15f && Vector2.Angle(touchpadAxes, Vector2.up) <= 45f)
            {
                __instance.m_flippedUp = !__instance.m_flippedUp;
                SM.PlayCoreSound(FVRPooledAudioType.GenericClose, __instance.AudEvent_Flip, __instance.gameObject.transform.position);
            }
            FVRFireArmAttachmentInterface_UpdateInteraction(__instance, hand);
            return false;
        }
    }
}
