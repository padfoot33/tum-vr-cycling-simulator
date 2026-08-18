using System.Collections.Generic;
using UnityEngine;

namespace CyclingExperiment.AI
{
    /// <summary>
    /// Marks a campus traffic car as pooled. Used on recycle without scene search.
    /// </summary>
    public class PooledAmbientCar : MonoBehaviour
    {
        public GameObject SourcePrefab;
        public AmbientVehiclePool Pool;
    }

    /// <summary>
    /// Reusable inactive cars under Ambient_Vehicle_Pool. No Destroy while playing.
    /// </summary>
    public class AmbientVehiclePool
    {
        public const string RootName = "Ambient_Vehicle_Pool";

        private Transform _root;
        private IReadOnlyList<GameObject> _prefabs;
        private int _cap;
        private int _created;
        private readonly List<GameObject> _inactive = new List<GameObject>(48);

        public int Cap => _cap;
        public int Created => _created;
        public int InactiveCount => _inactive.Count;

        public void Bind(Transform owner, IReadOnlyList<GameObject> prefabs, int cap)
        {
            _prefabs = prefabs;
            _cap = Mathf.Max(1, cap);
            if (_root == null)
            {
                Transform existing = owner != null ? owner.Find(RootName) : null;
                if (existing != null) _root = existing;
                else
                {
                    var go = new GameObject(RootName);
                    if (owner != null) go.transform.SetParent(owner, false);
                    _root = go.transform;
                }
            }
        }

        public void Prewarm(int count)
        {
            if (_prefabs == null || _prefabs.Count == 0) return;
            int target = Mathf.Min(count, _cap);
            int guard = 0;
            while (_created < target && guard < target + 8)
            {
                guard++;
                GameObject prefab = _prefabs[_created % _prefabs.Count];
                if (prefab == null)
                {
                    _created++;
                    continue;
                }

                GameObject created = CreateInactive(prefab);
                if (created != null) _inactive.Add(created);
            }
        }

        public GameObject Rent(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            GameObject vehicle = TakeInactive(prefab);
            if (vehicle == null) vehicle = CreateInactive(prefab);
            if (vehicle == null) vehicle = TakeAnyInactive();
            if (vehicle == null) return null;

            vehicle.transform.SetPositionAndRotation(position, rotation);
            vehicle.SetActive(true);
            return vehicle;
        }

        public void Release(GameObject vehicle)
        {
            if (vehicle == null) return;

            var ai = vehicle.GetComponent<SmartVehicleAI>();
            if (ai != null) ai.enabled = false;

            vehicle.SetActive(false);
            if (_root != null) vehicle.transform.SetParent(_root, true);
            if (!_inactive.Contains(vehicle)) _inactive.Add(vehicle);
        }

        public void ReleaseAll(IList<GameObject> vehicles)
        {
            if (vehicles == null) return;
            for (int i = 0; i < vehicles.Count; i++)
            {
                Release(vehicles[i]);
            }
        }

        private GameObject TakeInactive(GameObject prefab)
        {
            for (int i = _inactive.Count - 1; i >= 0; i--)
            {
                GameObject candidate = _inactive[i];
                if (candidate == null)
                {
                    _inactive.RemoveAt(i);
                    continue;
                }

                var member = candidate.GetComponent<PooledAmbientCar>();
                if (member != null && member.SourcePrefab == prefab)
                {
                    _inactive.RemoveAt(i);
                    return candidate;
                }
            }

            return null;
        }

        private GameObject TakeAnyInactive()
        {
            for (int i = _inactive.Count - 1; i >= 0; i--)
            {
                GameObject candidate = _inactive[i];
                _inactive.RemoveAt(i);
                if (candidate != null) return candidate;
            }

            return null;
        }

        private GameObject CreateInactive(GameObject prefab)
        {
            if (prefab == null || _created >= _cap) return null;

            GameObject vehicle = Object.Instantiate(prefab, _root);
            _created++;
            VehicleRuntimeFactory.Prepare(vehicle, disableRigidbody: true);

            var member = vehicle.GetComponent<PooledAmbientCar>();
            if (member == null) member = vehicle.AddComponent<PooledAmbientCar>();
            member.SourcePrefab = prefab;
            member.Pool = this;

            vehicle.SetActive(false);
            return vehicle;
        }
    }
}
