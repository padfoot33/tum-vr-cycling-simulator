using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace tum_bus_controller
{
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(Transform))]
    public class TaxiDoorController : MonoBehaviour
    {
        public bool enableDebugLogs = false;

        private Animator animator;
        private GameObject ramp;

        [SerializeField]
        private List<GameObject> doorList = new List<GameObject>();

        // Properties
        public List<GameObject> DoorList
        {
            get { return doorList; }
        }

        public List<Vector3> DoorPositions
        {
            get
            {
                List<Vector3> doorPositions = new List<Vector3>();
                foreach (GameObject door in doorList)
                {
                    doorPositions.Add(door.transform.position);
                }
                return doorPositions;
            }
        }

        void Start()
        {
            animator = GetComponent<Animator>();
            ramp = transform.Find("Ramp").gameObject;
            CloseDoors();
        }

        public void OpenDoors()
        {
            animator.SetBool("isDoorOpen", true);

            ramp.SetActive(true);
            
            if (enableDebugLogs)
            {
                Debug.Log("Bus Taxi doors opened.");
            }
        }

        public void CloseDoors()
        {
            animator.SetBool("isDoorOpen", false);
            
            ramp.SetActive(false);

            if (enableDebugLogs)
            {
                Debug.Log("Bus Taxi doors closed.");
            }
        }


        // EDITOR
        // [CustomEditor(typeof(TaxiDoorController))]
        // public class TaxiDoorControllerEditor : Editor
        // {
        //     public override void OnInspectorGUI()
        //     {
        //         DrawDefaultInspector();
        //         TaxiDoorController myScript = (TaxiDoorController)target;

        //         GUILayout.Label("Door Actions (Only work in Play Mode)");
        //         if (GUILayout.Button("Open Doors"))
        //         {
        //             myScript.OpenDoors();
        //         }
        //         if(GUILayout.Button("Close Doors"))
        //         {
        //             myScript.CloseDoors();
        //         }

        //     }
        // }

    }
}
