using System.Reflection;
using UnityEngine;
using UnityEngine.Audio;
using BepInEx;
using HarmonyLib;
using FistVR;

using System;
using Sodalite;
using Sodalite.Api;

namespace FasterSpectator
{
	[BepInPlugin("HLin-FasterSpectator", "FasterSpectator", "1.0.0")]
	[BepInDependency("nrgill28.Sodalite", "1.1.0")]
	public class SpectatorMode : BaseUnityPlugin
	{
		// Token: 0x06000001 RID: 1 RVA: 0x00002050 File Offset: 0x00000250
		public void Awake()
		{
			Harmony.CreateAndPatchAll(typeof(SpectatorMode), null);
			WristMenuAPI.Buttons.Add(new WristMenuButton("Increase Player Scale", new ButtonClickEvent(this.IncreaseScale)));
			WristMenuAPI.Buttons.Add(new WristMenuButton("Decrease Player Scale", new ButtonClickEvent(this.DecreaseScale)));
			WristMenuAPI.Buttons.Add(new WristMenuButton("Set Spectator", new ButtonClickEvent(this.SetSpectatorMode)));
			WristMenuAPI.Buttons.Add(new WristMenuButton("Undo Spectator", new ButtonClickEvent(this.UndoSpectatorMode)));
		}

		// Token: 0x06000002 RID: 2 RVA: 0x000020F4 File Offset: 0x000002F4
		public void IncreaseScale(object sender, ButtonClickEventArgs args)
		{
			GM.CurrentPlayerRoot.transform.localScale += Vector3.one;
			WristMenuAPI.Instance.transform.localScale = GM.CurrentPlayerRoot.transform.localScale;
		}

		// Token: 0x06000003 RID: 3 RVA: 0x00002144 File Offset: 0x00000344
		public void DecreaseScale(object sender, ButtonClickEventArgs args)
		{
			bool flag = GM.CurrentPlayerRoot.transform.localScale.x < 2f;
			if (flag)
			{
				GM.CurrentPlayerRoot.transform.localScale = Vector3.one;
			}
			else
			{
				GM.CurrentPlayerRoot.transform.localScale -= Vector3.one;
			}
			WristMenuAPI.Instance.transform.localScale = GM.CurrentPlayerRoot.transform.localScale;
		}

		// Token: 0x06000004 RID: 4 RVA: 0x000021C7 File Offset: 0x000003C7
		public void SetSpectatorMode(object sender, ButtonClickEventArgs args)
		{
			GM.CurrentMovementManager.Mode = (FVRMovementManager.MovementMode)10;
			GM.CurrentPlayerBody.DisableHitBoxes();
			GM.CurrentPlayerBody.SetPlayerIFF(-3);
		}

		// Token: 0x06000005 RID: 5 RVA: 0x000021EE File Offset: 0x000003EE
		public void UndoSpectatorMode(object sender, ButtonClickEventArgs args)
		{
			GM.CurrentMovementManager.Mode = GM.Options.MovementOptions.CurrentMovementMode;
			GM.CurrentPlayerBody.EnableHitBoxes();
			GM.CurrentPlayerBody.SetPlayerIFF(0);
		}

		// Token: 0x06000006 RID: 6 RVA: 0x00002224 File Offset: 0x00000424
		[HarmonyPatch(typeof(FVRMovementManager), "FU")]
		[HarmonyPostfix]
		public static void UpdateSpectatorMode(FVRMovementManager __instance)
		{
			bool flag = __instance.Mode == (FVRMovementManager.MovementMode)10;
			if (flag)
			{
				Vector3 vector = Vector3.zero;
				foreach (FVRViveHand fvrviveHand in __instance.Hands)
				{
					bool isInStreamlinedMode = fvrviveHand.IsInStreamlinedMode;
					if (isInStreamlinedMode)
					{
						bool flag2 = (fvrviveHand.CMode == ControlMode.Index || fvrviveHand.CMode == ControlMode.WMR) && fvrviveHand.Input.Secondary2AxisInputAxes.y > 0.1f;
						if (flag2)
						{
							vector += fvrviveHand.PointingTransform.forward * fvrviveHand.Input.Secondary2AxisInputAxes.y * Time.fixedDeltaTime * GM.CurrentPlayerRoot.localScale.y;
						}
						else
						{
							bool flag3 = fvrviveHand.Input.TouchpadAxes.y > 0.1f;
							if (flag3)
							{
								vector += fvrviveHand.PointingTransform.forward * fvrviveHand.Input.TouchpadAxes.y * Time.fixedDeltaTime * GM.CurrentPlayerRoot.localScale.y;
							}
						}
					}
					else
					{
						bool bybuttonPressed = fvrviveHand.Input.BYButtonPressed;
						if (bybuttonPressed)
						{
							vector += fvrviveHand.PointingTransform.forward * Time.fixedDeltaTime * GM.CurrentPlayerRoot.localScale.y;
						}
					}
				}
				GM.CurrentPlayerRoot.position += vector * 10;
			}
		}
	}
}