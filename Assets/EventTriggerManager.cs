using System.Collections.Generic;
using UnityEngine;

public class EventTriggerManager : MonoBehaviour
{
    //[Header("谁可以触发（任选其一）")]
    [SerializeField] private string triggeringTag = "Player";
    [SerializeField] private GameObject specificObject;

    //[Header("第一次碰撞（Zone = First）激活的对象集合")]
    [SerializeField] private List<GameObject> batchAActivate = new List<GameObject>();

    //[Header("第二次碰撞（Zone = Second）激活的对象集合")]
    [SerializeField] private List<GameObject> batchBActivate = new List<GameObject>();

    //[Header("可选项")]
    //[Tooltip("进入First时自动关闭B；进入Second时自动关闭A")]
    [SerializeField] private bool autoDeactivateOpposite = true;

    //[Tooltip("若列表里给的是子物体，则自动寻找其最上层处于关闭状态的祖先并激活它")]
    [SerializeField] private bool autoPromoteToTopInactiveAncestor = true;

    private void Start()
    {
        // 开局确保两组都关着，防止忘记在Inspector里手动关
        SetActive(batchAActivate, false);
        SetActive(batchBActivate, false);
    }

    public void HandleZoneEnter(TriggerZone.ZoneType zone, Collider other)
    {
        if (!IsValidTrigger(other.gameObject)) return;

        switch (zone)
        {
            case TriggerZone.ZoneType.First:
                SetActive(batchAActivate, true);
                if (autoDeactivateOpposite) SetActive(batchBActivate, false);
                break;

            case TriggerZone.ZoneType.Second:
                SetActive(batchAActivate, false);
                SetActive(batchBActivate, true);
                break;
        }
    }

    private bool IsValidTrigger(GameObject obj)
    {
        if (specificObject != null && obj != specificObject) return false;
        if (!string.IsNullOrEmpty(triggeringTag) && !obj.CompareTag(triggeringTag)) return false;
        return true;
    }

    private GameObject ResolveActivatable(GameObject go)
    {
        if (!autoPromoteToTopInactiveAncestor || go == null) return go;

        // 向上寻找：一直爬到“父节点已是 activeSelf=true”或到根为止
        Transform candidate = go.transform;
        Transform p = candidate.parent;
        while (p != null && !p.gameObject.activeSelf)
        {
            candidate = p;
            p = p.parent;
        }
        return candidate.gameObject;
    }

    private void SetActive(List<GameObject> list, bool active)
    {
        if (list == null) return;
        foreach (var go in list)
        {
            if (go == null) continue;
            var target = ResolveActivatable(go);
            target.SetActive(active);
        }
    }
}
