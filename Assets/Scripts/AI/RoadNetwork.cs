using System.Collections.Generic;
using UnityEngine;

namespace CyclingExperiment.AI
{
    /// <summary>
    /// Scene-authored directed road graph. Leftover from an earlier traffic approach; cars no longer drive it.
    /// </summary>
    [DefaultExecutionOrder(-180)]
    public class RoadNetwork : MonoBehaviour
    {
        public const string RootName = "Campus_Road_Network";
        public const float SnapRadius = 8f;
        public const float MergeRadius = 15f;

        public static RoadNetwork Instance { get; private set; }

        [SerializeField] private List<RoadEdge> edges = new List<RoadEdge>();

        private readonly Dictionary<RoadNode, List<RoadEdge>> _outgoing = new Dictionary<RoadNode, List<RoadEdge>>();
        private readonly List<RoadEdge> _route2 = new List<RoadEdge>();
        private RoadEdge[] _edgeArray = System.Array.Empty<RoadEdge>();

        public IReadOnlyList<RoadEdge> Edges => edges;
        public int EdgeCount => edges != null ? edges.Count : 0;

        private void Awake()
        {
            Instance = this;
            RefreshRuntimeCache();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void RefreshRuntimeCache()
        {
            _outgoing.Clear();
            _route2.Clear();
            if (edges == null) edges = new List<RoadEdge>();

            for (int i = edges.Count - 1; i >= 0; i--)
            {
                RoadEdge edge = edges[i];
                if (edge == null || !edge.IsValid)
                {
                    edges.RemoveAt(i);
                    continue;
                }

                edge.RebuildPolyline();
                if (!_outgoing.TryGetValue(edge.from, out List<RoadEdge> list))
                {
                    list = new List<RoadEdge>(4);
                    _outgoing[edge.from] = list;
                }

                list.Add(edge);
                if (edge.isRoute2) _route2.Add(edge);
            }

            _edgeArray = edges.ToArray();
        }

        public IReadOnlyList<RoadEdge> GetOutgoing(RoadNode node)
        {
            if (node == null) return System.Array.Empty<RoadEdge>();
            return _outgoing.TryGetValue(node, out List<RoadEdge> list) ? list : System.Array.Empty<RoadEdge>();
        }

        public RoadEdge PickNext(RoadEdge incoming)
        {
            if (incoming == null || incoming.to == null) return null;

            IReadOnlyList<RoadEdge> options = GetOutgoing(incoming.to);
            RoadEdge fallback = null;
            int valid = 0;
            RoadEdge lastValid = null;
            for (int i = 0; i < options.Count; i++)
            {
                RoadEdge option = options[i];
                if (option == null || option == incoming) continue;
                if (option.IsReverseOf(incoming))
                {
                    fallback = option;
                    continue;
                }

                lastValid = option;
                valid++;
            }

            if (valid == 0) return fallback;
            if (valid == 1) return lastValid;

            int pick = Random.Range(0, valid);
            for (int i = 0; i < options.Count; i++)
            {
                RoadEdge option = options[i];
                if (option == null || option == incoming || option.IsReverseOf(incoming)) continue;
                if (pick == 0) return option;
                pick--;
            }

            return lastValid;
        }

        public bool TryFindNearestEdge(Vector3 world, out RoadEdge edge, out float distanceAlong, float maxLateral = 18f)
        {
            edge = null;
            distanceAlong = 0f;
            float best = maxLateral;
            for (int i = 0; i < _edgeArray.Length; i++)
            {
                RoadEdge candidate = _edgeArray[i];
                if (candidate == null || candidate.Length < 1f) continue;
                float along = candidate.ClosestDistanceAlong(world, out float lateral);
                if (lateral < best)
                {
                    best = lateral;
                    edge = candidate;
                    distanceAlong = along;
                }
            }

            return edge != null;
        }

        public bool TryPickSpawn(bool preferRoute2, System.Func<Vector3, bool> isClear, out RoadEdge edge, out float distanceAlong, out Vector3 position, out Quaternion rotation)
        {
            edge = null;
            distanceAlong = 0f;
            position = Vector3.zero;
            rotation = Quaternion.identity;

            if (preferRoute2 && TryPickSpawnFrom(_route2, 0.05f, 0.35f, isClear, out edge, out distanceAlong, out position, out rotation))
                return true;

            return TryPickSpawnFrom(_edgeArray, 0.08f, 0.55f, isClear, out edge, out distanceAlong, out position, out rotation);
        }

        public void CollectApproachingEdges(Vector3 junction, float radius, List<RoadEdge> results)
        {
            results.Clear();
            float radiusSq = radius * radius;
            for (int i = 0; i < _edgeArray.Length; i++)
            {
                RoadEdge edge = _edgeArray[i];
                if (edge == null || !edge.IsValid) continue;

                Vector3 to = edge.to.Position - junction;
                to.y = 0f;
                if (to.sqrMagnitude > radiusSq) continue;

                Vector3 from = edge.from.Position - junction;
                from.y = 0f;
                if (from.sqrMagnitude + 4f <= to.sqrMagnitude) continue;
                results.Add(edge);
            }
        }

        public IReadOnlyList<RoadEdge> Route2Edges => _route2;

        /// <summary>
        /// Rebuilds nodes and directed edges for the campus arteries. Overwrites existing graph.
        /// </summary>
        public void RebuildFromCampusSeeds(Transform destRoot)
        {
            List<Vector3> snap = CollectSnapPoints(destRoot);
            RebuildFromCampusSeeds(snap);
        }

        public void RebuildFromCampusSeeds(IReadOnlyList<Vector3> snapPoints)
        {
            ClearGraph();

            var seeds = CampusArteries();
            var rawVerts = new List<Vector3>(64);
            var polylines = new List<SeedPolyline>(seeds.Length);

            for (int s = 0; s < seeds.Length; s++)
            {
                SeedPolyline seed = seeds[s];
                var snapped = new Vector3[seed.points.Length];
                for (int i = 0; i < seed.points.Length; i++)
                {
                    snapped[i] = Snap(seed.points[i], snapPoints);
                    rawVerts.Add(snapped[i]);
                }

                seed.points = snapped;
                polylines.Add(seed);
            }

            int[] cluster = Cluster(rawVerts, MergeRadius);
            var nodesByCluster = new Dictionary<int, RoadNode>();
            int nodeIndex = 0;
            for (int i = 0; i < rawVerts.Count; i++)
            {
                int c = cluster[i];
                if (nodesByCluster.ContainsKey(c)) continue;
                RoadNode node = CreateNode($"Node_{nodeIndex++}", ClusterCentroid(rawVerts, cluster, c));
                nodesByCluster[c] = node;
            }

            var vertOffset = 0;
            for (int s = 0; s < polylines.Count; s++)
            {
                SeedPolyline seed = polylines[s];
                for (int i = 0; i < seed.points.Length - 1; i++)
                {
                    RoadNode from = nodesByCluster[cluster[vertOffset + i]];
                    RoadNode to = nodesByCluster[cluster[vertOffset + i + 1]];
                    AddEdgeUnique($"{seed.name}_{i}", from, to, seed.laneOffset, seed.isRoute2);
                    if (!seed.oneWay)
                    {
                        AddEdgeUnique($"{seed.name}_{i}_rev", to, from, seed.laneOffset, seed.isRoute2);
                    }
                }

                vertOffset += seed.points.Length;
            }

            RefreshRuntimeCache();
        }

        private void AddEdgeUnique(string id, RoadNode from, RoadNode to, float laneOffset, bool isRoute2)
        {
            if (from == null || to == null || from == to) return;
            for (int i = 0; i < edges.Count; i++)
            {
                if (edges[i] != null && edges[i].from == from && edges[i].to == to) return;
            }

            edges.Add(new RoadEdge
            {
                id = id,
                from = from,
                to = to,
                cruiseSpeed = 9.5f,
                laneOffset = laneOffset,
                isRoute2 = isRoute2
            });
        }

        private RoadNode CreateNode(string nodeName, Vector3 position)
        {
            var go = new GameObject(nodeName);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            var node = go.AddComponent<RoadNode>();
            node.NodeId = nodeName;
            return node;
        }

        private void ClearGraph()
        {
            edges = new List<RoadEdge>();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child == null) continue;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    Object.DestroyImmediate(child.gameObject);
                    continue;
                }
#endif
                Object.Destroy(child.gameObject);
            }
        }

        private bool TryPickSpawnFrom(IList<RoadEdge> pool, float tMin, float tMax, System.Func<Vector3, bool> isClear, out RoadEdge edge, out float distanceAlong, out Vector3 position, out Quaternion rotation)
        {
            edge = null;
            distanceAlong = 0f;
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (pool == null || pool.Count == 0) return false;

            for (int attempt = 0; attempt < 24; attempt++)
            {
                RoadEdge candidate = pool[Random.Range(0, pool.Count)];
                if (candidate == null || candidate.Length < 16f) continue;

                float t = Random.Range(tMin, tMax);
                float along = candidate.Length * t;
                if (!candidate.Sample(along, out Vector3 pos, out Vector3 forward)) continue;
                if (isClear != null && !isClear(pos)) continue;

                edge = candidate;
                distanceAlong = along;
                position = pos;
                rotation = Quaternion.LookRotation(forward);
                return true;
            }

            return false;
        }

        private static List<Vector3> CollectSnapPoints(Transform destRoot)
        {
            var snap = new List<Vector3>();
            if (destRoot == null) return snap;
            for (int i = 0; i < destRoot.childCount; i++)
            {
                Transform child = destRoot.GetChild(i);
                if (child != null) snap.Add(child.position);
            }

            return snap;
        }

        private static Vector3 Snap(Vector3 point, IReadOnlyList<Vector3> snapPoints)
        {
            if (snapPoints == null || snapPoints.Count == 0) return point;
            float best = SnapRadius * SnapRadius;
            Vector3 snapped = point;
            for (int i = 0; i < snapPoints.Count; i++)
            {
                Vector3 delta = snapPoints[i] - point;
                delta.y = 0f;
                float mag = delta.sqrMagnitude;
                if (mag < best)
                {
                    best = mag;
                    snapped = snapPoints[i];
                    snapped.y = point.y;
                }
            }

            return snapped;
        }

        private static int[] Cluster(List<Vector3> verts, float radius)
        {
            int n = verts.Count;
            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;

            int Find(int x)
            {
                while (parent[x] != x) x = parent[x] = parent[parent[x]];
                return x;
            }

            void Union(int a, int b)
            {
                a = Find(a);
                b = Find(b);
                if (a != b) parent[b] = a;
            }

            float r2 = radius * radius;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    Vector3 d = verts[i] - verts[j];
                    d.y = 0f;
                    if (d.sqrMagnitude <= r2) Union(i, j);
                }
            }

            for (int i = 0; i < n; i++) parent[i] = Find(i);
            return parent;
        }

        private static Vector3 ClusterCentroid(List<Vector3> verts, int[] cluster, int id)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < verts.Count; i++)
            {
                if (cluster[i] != id) continue;
                sum += verts[i];
                count++;
            }

            return count > 0 ? sum / count : sum;
        }

        private struct SeedPolyline
        {
            public string name;
            public bool oneWay;
            public bool isRoute2;
            public float laneOffset;
            public Vector3[] points;
        }

        private static SeedPolyline[] CampusArteries()
        {
            const float y = 0.2f;
            return new[]
            {
                new SeedPolyline
                {
                    name = "Gabelsberger",
                    oneWay = false,
                    laneOffset = 2.4f,
                    points = new[]
                    {
                        new Vector3(436f, y, -80f),
                        new Vector3(436f, y, -20f),
                        new Vector3(436f, y, 80f),
                        new Vector3(430f, y, 174f)
                    }
                },
                new SeedPolyline
                {
                    name = "Arcis",
                    oneWay = false,
                    laneOffset = 2.4f,
                    points = new[]
                    {
                        new Vector3(300f, y, 172f),
                        new Vector3(430f, y, 174f),
                        new Vector3(580f, y, 170f)
                    }
                },
                new SeedPolyline
                {
                    name = "Luisen",
                    oneWay = false,
                    laneOffset = 2.4f,
                    points = new[]
                    {
                        new Vector3(580f, y, 80f),
                        new Vector3(580f, y, 170f)
                    }
                },
                new SeedPolyline
                {
                    name = "Theresien",
                    oneWay = false,
                    laneOffset = 2.4f,
                    points = new[]
                    {
                        new Vector3(436f, y, 80f),
                        new Vector3(580f, y, 80f)
                    }
                },
                new SeedPolyline
                {
                    name = "Route2",
                    oneWay = true,
                    isRoute2 = true,
                    laneOffset = Route2Corridor.RightLaneMeters,
                    points = new[]
                    {
                        new Vector3(804.22f, y, 91.30f),
                        new Vector3(700.20f, y, 135.50f),
                        new Vector3(646.74f, y, 166.55f),
                        new Vector3(329.49f, y, 295.19f)
                    }
                }
            };
        }

        private void OnDrawGizmos()
        {
            if (edges == null) return;
            for (int i = 0; i < edges.Count; i++)
            {
                RoadEdge edge = edges[i];
                if (edge == null || !edge.IsValid) continue;
                if (edge.Polyline == null || edge.Polyline.Length < 2)
                {
                    edge.RebuildPolyline();
                }

                if (edge.Polyline == null || edge.Polyline.Length < 2) continue;

                Gizmos.color = edge.isRoute2
                    ? new Color(1f, 0.55f, 0.15f, 0.95f)
                    : new Color(0.25f, 0.9f, 0.45f, 0.9f);

                for (int p = 0; p < edge.Polyline.Length - 1; p++)
                {
                    Vector3 a = edge.Polyline[p] + Vector3.up * 0.4f;
                    Vector3 b = edge.Polyline[p + 1] + Vector3.up * 0.4f;
                    Gizmos.DrawLine(a, b);
                    Vector3 mid = (a + b) * 0.5f;
                    Vector3 dir = (b - a);
                    dir.y = 0f;
                    if (dir.sqrMagnitude < 0.01f) continue;
                    dir.Normalize();
                    Vector3 right = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 160f, 0f) * Vector3.forward;
                    Vector3 left = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, -160f, 0f) * Vector3.forward;
                    Gizmos.DrawRay(mid, right * 1.4f);
                    Gizmos.DrawRay(mid, left * 1.4f);
                }
            }
        }
    }
}
