using BepInEx;
using HarmonyLib;
using RealPOV.Core;
using Studio;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

[assembly: System.Reflection.AssemblyFileVersion(RealPOV.Koikatu.RealPOV.Version)]

namespace RealPOV.Koikatu
{
    [BepInPlugin(GUID, PluginName, Version)]
    public class RealPOV : RealPOVCore
    {
        public const string Version = "1.0.4." + BuildNumber.Version;

        private static int backupLayer;
        private static ChaControl currentChara;
        private static Queue<ChaControl> charaQueue;
        private bool isStudio = Paths.ProcessName == "CharaStudio";

        protected override void Awake()
        {
            base.Awake();
            Harmony.CreateAndPatchAll(GetType());

            SceneManager.activeSceneChanged += (arg0, scene) => charaQueue = null;
        }

        internal override void EnablePOV()
        {

            if(isStudio)
            {
                var selectedCharas = GuideObjectManager.Instance.selectObjectKey.Select(x => Studio.Studio.GetCtrlInfo(x) as OCIChar).Where(x => x != null).ToList();
                if(selectedCharas.Count > 0)
                    currentChara = selectedCharas.First().charInfo;
                else
                    Logger.LogMessage("Select a character in workspace to enter its POV");
            }
            else
            {
                void CreateQueue()
                {
                    charaQueue = new Queue<ChaControl>();
                    foreach (ChaControl chara in FindObjectsOfType<ChaControl>())
                        charaQueue.Enqueue(chara);
                    if (charaQueue.Count > 0)
                    {
                        currentChara = charaQueue.Dequeue();
                        charaQueue.Enqueue(currentChara);
                    }
                }

                currentChara = null;
                if(charaQueue == null)
                {
                    CreateQueue();
                }
                else
                {
                    while (charaQueue.Count > 0 && (currentChara = charaQueue.Dequeue()) == null) {}

                    if (currentChara != null)
                        charaQueue.Enqueue(currentChara);
                    else
                        CreateQueue();
                }
            }

            if(currentChara)
            {
                //foreach(var bone in currentChara.neckLookCtrl.neckLookScript.aBones)
                //    bone.neckBone.rotation = new Quaternion();

                GameCamera = Camera.main;

                base.EnablePOV();

                backupLayer = GameCamera.gameObject.layer;
                GameCamera.gameObject.layer = 0;
            }
        }

        internal override void DisablePOV()
        {
            base.DisablePOV();
            GameCamera.gameObject.layer = backupLayer;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(NeckLookControllerVer2), "LateUpdate")]
        private static bool ApplyRotation(NeckLookControllerVer2 __instance)
        {
            if(POVEnabled)
            {
                if(__instance.neckLookScript && currentChara.neckLookCtrl == __instance)
                {
                    __instance.neckLookScript.aBones[0].neckBone.rotation = Quaternion.identity;
                    __instance.neckLookScript.aBones[1].neckBone.rotation = Quaternion.identity;
                    __instance.neckLookScript.aBones[1].neckBone.Rotate(LookRotation);

                    var eyeObjs = currentChara.eyeLookCtrl.eyeLookScript.eyeObjs;
                    GameCamera.transform.position = Vector3.Lerp(eyeObjs[0].eyeTransform.position, eyeObjs[1].eyeTransform.position, 0.5f);
                    GameCamera.transform.rotation = currentChara.objHeadBone.transform.rotation;
                    GameCamera.transform.Translate(Vector3.forward * ViewOffset.Value);
                    GameCamera.fieldOfView = CurrentFOV;

                    return false;
                }
                else
                {
                    __instance.target = currentChara.eyeLookCtrl.transform;
                }
            }

            return true;
        }
    }
}
