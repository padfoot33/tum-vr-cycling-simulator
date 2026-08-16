using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SBPScripts.Simulator
{
    public class TPSCamSwitchSimulator : MonoBehaviour
    {
        public GameObject cyclist;
        public GameObject externalCharacter;
        BicycleSimulatorCamera bicycleCamera;
        BicycleSimulatorStatus bicycleStatus;
        void Start()
        {
            bicycleCamera = FindObjectOfType<BicycleSimulatorCamera>();
            bicycleStatus = FindObjectOfType<BicycleSimulatorStatus>();
        }
        void LateUpdate()
        {
            if (externalCharacter != null)
            {
                if (externalCharacter.activeInHierarchy)
                {
                    bicycleCamera.target = externalCharacter.transform;
                }
                else
            {
                bicycleCamera.target = cyclist.transform.root.transform;
            }
            }
            
            
            if (bicycleStatus.dislodged && bicycleStatus.instantiatedRagdoll!=null)
            {
                bicycleCamera.target = bicycleStatus.instantiatedRagdoll.transform.Find("mixamorig:Hips").gameObject.transform;
            }
            else if(externalCharacter==null)
            {
                bicycleCamera.target = cyclist.transform.root.transform;
            }
        }
    }
}
