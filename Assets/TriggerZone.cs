// TriggerZone.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TriggerZone : MonoBehaviour
{
    public enum ZoneType { First, Second }

    //[Header("当前触发区类型")]
    public ZoneType zoneType = ZoneType.First;

    //[Header("事件管理器（拖到这里）")]
    public EventTriggerManager manager;

    private void Reset()
    {
      
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (manager == null) return;
        manager.HandleZoneEnter(zoneType, other);
    }
}

