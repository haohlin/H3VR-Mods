using System;
using UnityEngine;
using FistVR;

namespace HLin_Mods.BubbleLevelSet
{
    public class BubbleLevel : MonoBehaviour {

        [Header("BaseObject")]
        public GameObject baseObject = null;
        [Header("FVRAttachment")]
        public FVRFireArmAttachment attachment = null;
        [Header("LevelBubble")]
        public GameObject level_bubble = null;

        // Use this for initialization
        private void Start()
        {
        }

        // Update is called once per frame
        private void Update()
        {
            // Debug.Log(String.Format("current global rotaion is {0}, {1}, {2}", baseObject.transform.eulerAngles.x, baseObject.transform.eulerAngles.y, baseObject.transform.eulerAngles.z));
            // Debug.Log(String.Format("current local rotation is {0}, {1}, {2}", baseObject.transform.localEulerAngles.x, baseObject.transform.localEulerAngles.y, baseObject.transform.localEulerAngles.z));
            // Debug.Log(String.Format("current local position is {0}, {1}, {2}", level_bubble.transform.localPosition.x, level_bubble.transform.localPosition.y, level_bubble.transform.localPosition.z));
            // if (attachment.curMount != null)
            // {
            //     Debug.Log(String.Format("attachment global rotation is {0}, {1}, {2}", attachment.curMount.GetRootMount().transform.eulerAngles.x, attachment.curMount.GetRootMount().transform.eulerAngles.y, attachment.curMount.GetRootMount().transform.eulerAngles.z));
            //     Debug.Log(String.Format("attachment local rotation is {0}, {1}, {2}", attachment.curMount.GetRootMount().transform.localEulerAngles.x, attachment.curMount.GetRootMount().transform.localEulerAngles.y, attachment.curMount.GetRootMount().transform.localEulerAngles.z));
            // }
            
            float cur_z_angle;
            if (attachment.curMount != null)
            {
                cur_z_angle = attachment.curMount.GetRootMount().transform.eulerAngles.z;
				if (180f < cur_z_angle && cur_z_angle < 360f)
				{
					cur_z_angle -= 360f;
				}
				cur_z_angle = -cur_z_angle;
            }
            else
            {
                cur_z_angle = baseObject.transform.localEulerAngles.z;
				if (180f < cur_z_angle && cur_z_angle < 360f)
				{
					cur_z_angle -= 360f;
				}
            }

            Vector3 tempPos = level_bubble.transform.localPosition;
            tempPos.z = Mathf.Clamp(cur_z_angle, -3.3f, 3.3f);
            level_bubble.transform.localPosition = tempPos;
        }
    }
}
