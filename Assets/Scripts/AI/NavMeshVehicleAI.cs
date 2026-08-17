using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace CyclingExperiment.AI
{
    /// <summary>
    /// Ambient car. Route is calculated along the current road corridor, not every frame.
    /// </summary>
    public class NavMeshVehicleAI : MonoBehaviour
    {
        public const float Route2CenterX = 804.2f;
        public const float Route2HalfWidth = 24f;
        public const float Route2MinZ = 55f;
        public const float Route2MaxZ = 250f;
        public const float Route2RightLaneMeters = 3.2f;
        private static Vector3 s_route2Heading = Vector3.forward;
        private static bool s_route2HeadingReady;

        public static Vector3 Route2Heading
        {
            get
            {
                EnsureRoute2Heading();
                return s_route2Heading;
            }
        }

        [SerializeField] private float cruiseSpeed = 9.5f;
        [SerializeField] private float rightLaneOffset = 3.2f;
        [SerializeField] private float followCheckInterval = 0.25f;
        [SerializeField] private float forwardLookaheadDistance = 12f;
        [SerializeField] private float stoppingBuffer = 3.5f;
        [SerializeField] private float forwardDetectionRadius = 0.65f;
        [SerializeField] private float groundBias = 0.16f;

        private enum RecoverPhase
        {
            None,
            Reverse,
            Sidestep
        }

        private NavMeshAgent _agent;
        private NavMeshPath _cachedPath;
        private float _currentTargetSpeed;
        private float _followTimer;
        private float _repathCooldown;
        private bool _isExperimentStressVehicle;
        private Transform _currentDestination;
        private Vector3? _queuedDest;
        private float _stoppedTime;
        private RecoverPhase _recoverPhase;
        private float _recoverTime;
        private float _dbgWayAt;

        private static readonly System.Collections.Generic.List<NavMeshVehicleAI> Active =
            new System.Collections.Generic.List<NavMeshVehicleAI>(48);

        public Transform ClaimedDestination => _currentDestination;

        public float CruiseSpeed
        {
            get => cruiseSpeed;
            set => cruiseSpeed = value;
        }

        public bool IsExperimentStressVehicle
        {
            get => _isExperimentStressVehicle;
            set => _isExperimentStressVehicle = value;
        }

        public static void ResetRoute2Heading()
        {
            s_route2HeadingReady = false;
            s_route2Heading = Vector3.forward;
        }

        public static void EnsureRoute2Heading()
        {
            if (s_route2HeadingReady) return;
            Vector3 origin = new Vector3(Route2CenterX, 0.2f, 91.3f);
            Vector3 target = origin + Vector3.left * 80f;
            if (TrafficDestinationSet.Instance != null)
            {
                Transform start = TrafficDestinationSet.Instance.FindByName("Dest_67");
                Transform next = TrafficDestinationSet.Instance.FindByName("Dest_66");
                if (next == null) next = TrafficDestinationSet.Instance.FindByName("Dest_62");
                if (start != null) origin = start.position;
                if (next != null) target = next.position;
            }

            Vector3 heading = target - origin;
            heading.y = 0f;
            s_route2Heading = heading.sqrMagnitude > 0.01f ? heading.normalized : Vector3.left;
            s_route2HeadingReady = true;
        }

        public static Vector3 PickWalkableStreetHeading(Vector3 origin)
        {
            float north = MeasureWalkable(origin, Vector3.forward, 80f);
            if (north >= 12f) return Vector3.forward;

            Vector3[] dirs =
            {
                Vector3.right,
                Vector3.left,
                new Vector3(0.7f, 0f, 0.7f).normalized,
                new Vector3(-0.7f, 0f, 0.7f).normalized,
                new Vector3(0.7f, 0f, -0.3f).normalized,
                new Vector3(-0.7f, 0f, -0.3f).normalized
            };
            float best = north;
            Vector3 bestDir = Vector3.forward;
            for (int i = 0; i < dirs.Length; i++)
            {
                float walk = MeasureWalkable(origin, dirs[i], 80f);
                if (walk > best)
                {
                    best = walk;
                    bestDir = dirs[i];
                }
            }

            return bestDir;
        }

        public static bool TryWalkAlongMesh(Vector3 from, Vector3 heading, float distance, out Vector3 dest)
        {
            dest = from;
            heading.y = 0f;
            if (heading.sqrMagnitude < 0.01f) return false;
            heading.Normalize();
            if (!NavMesh.SamplePosition(from, out NavMeshHit start, 4f, NavMesh.AllAreas)) return false;

            Vector3 pos = start.position;
            float stepped = 0f;
            const float step = 3.5f;
            while (stepped + 0.5f < distance)
            {
                Vector3 next = pos + heading * step;
                if (NavMesh.Raycast(pos, next, out NavMeshHit blocked, NavMesh.AllAreas))
                {
                    dest = blocked.position;
                    return stepped > 8f;
                }

                if (!NavMesh.SamplePosition(next, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
                {
                    dest = pos;
                    return stepped > 8f;
                }

                pos = hit.position;
                stepped += step;
            }

            dest = pos;
            return true;
        }

        public static Vector3 Route2CenterAt(float z)
        {
            Vector3 heading = Route2Heading;
            return new Vector3(Route2CenterX, 0f, 91.3f) + heading * (z - 91.3f);
        }

        public static Vector3 Route2RightLaneAt(float along)
        {
            Vector3 right = Vector3.Cross(Vector3.up, Route2Heading);
            Vector3 point = new Vector3(Route2CenterX, 0f, 91.3f) + Route2Heading * along + right * Route2RightLaneMeters;
            point.y = 1f;
            return point;
        }

        public static float Route2LaneOffset(Vector3 position)
        {
            Vector3 origin = new Vector3(Route2CenterX, 0f, 91.3f);
            Vector3 right = Vector3.Cross(Vector3.up, Route2Heading);
            return Vector3.Dot(position - origin, right);
        }

        public static bool IsRoute2Corridor(Vector3 position)
        {
            Vector3 origin = new Vector3(Route2CenterX, 0f, 91.3f);
            Vector3 delta = position - origin;
            delta.y = 0f;
            float along = Vector3.Dot(delta, Route2Heading);
            float lateral = Vector3.Dot(delta, Vector3.Cross(Vector3.up, Route2Heading));
            if (along >= -12f && along <= 650f && Mathf.Abs(lateral) < Route2HalfWidth)
                return true;
            return TrafficDestinationSet.Instance != null
                   && TrafficDestinationSet.Instance.ClosestChainIndex(position) >= 0;
        }

        public static bool TrySampleRightLane(Vector3 guess, out Vector3 position)
        {
            Vector3 right = Vector3.Cross(Vector3.up, Route2Heading);
            for (float extra = 0.4f; extra >= -1.6f; extra -= 0.8f)
            {
                Vector3 probe = guess + right * extra;
                if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 2.0f, NavMesh.AllAreas)) continue;
                if (Route2LaneOffset(hit.position) < 0.35f) continue;
                position = hit.position;
                return true;
            }

            position = guess;
            return false;
        }

        public void BindAgent(NavMeshAgent agent)
        {
            _agent = agent;
            if (_agent == null) return;

            rightLaneOffset = Mathf.Max(rightLaneOffset, Route2RightLaneMeters);
            _agent.speed = cruiseSpeed;
            _agent.acceleration = 8f;
            _agent.angularSpeed = 90f;
            _agent.radius = 1.1f;
            _agent.height = 1.0f;
            _agent.baseOffset = Mathf.Max(0f, _agent.baseOffset - groundBias);
            _agent.autoBraking = false;
            _agent.updatePosition = true;
            _agent.updateRotation = true;
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            _agent.avoidancePriority = 40;
            _cachedPath = new NavMeshPath();
            _currentTargetSpeed = cruiseSpeed;
        }

        private void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        public void AssignRoadCorridorRoute()
        {
            EnsureOnMesh();
            if (_agent == null || !_agent.isOnNavMesh) return;

            Transform avoid = FindClaimedDestinationAhead();
            Transform next = null;
            Vector3 pickForward = IsRoute2Corridor(transform.position) ? Route2Heading : transform.forward;
            if (TrafficDestinationSet.Instance != null &&
                TrafficDestinationSet.Instance.TryPickNext(transform.position, pickForward, _currentDestination, avoid, out next)
                && !IsSouthboundOnRoute2(transform.position, next.position))
            {
                _currentDestination = next;
                Vector3 dest = next.position;
                if (NavMesh.SamplePosition(dest, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
                    dest = hit.position;
                // #region agent log
                LogRouteAssign("dest", dest, next.name, false);
                // #endregion
                AssignSpawnRoute(dest);
                return;
            }

            bool rejectedSouth = next != null && IsSouthboundOnRoute2(transform.position, next.position);
            _currentDestination = null;
            Vector3 corridor = PickRoadCorridorDestination();
            // #region agent log
            LogRouteAssign(rejectedSouth ? "reject_south" : "corridor", corridor, next != null ? next.name : "none", rejectedSouth);
            // #endregion
            AssignSpawnRoute(corridor);
        }

        public void AssignSpawnRoute(Vector3 destination)
        {
            if (_agent == null || !_agent.isOnNavMesh) return;

            Vector3 from = transform.position;
            if (IsSouthboundOnRoute2(from, destination) ||
                (IsRoute2Corridor(from) && Vector3.Dot(destination - from, Route2Heading) < 4f))
            {
                destination = PickRoadCorridorDestination();
            }

            Vector3 toDest = destination - from;
            toDest.y = 0f;
            Vector3 forward = IsRoute2Corridor(from) ? Route2Heading : FlatForward();
            Vector3 approach = toDest.sqrMagnitude > 0.01f ? toDest.normalized : forward;
            destination = OffsetToRightLane(destination, approach);

            float turnAngle = toDest.sqrMagnitude > 0.01f ? Vector3.Angle(forward, toDest) : 0f;
            if (turnAngle > 35f && _queuedDest == null)
            {
                Vector3 via = from + forward * 22f + Vector3.Cross(Vector3.up, forward) * rightLaneOffset;
                if (TrySampleKeepRight(via, forward, out Vector3 viaPos)
                    && !IsSouthboundOnRoute2(from, viaPos)
                    && !IsSouthboundOnRoute2(viaPos, destination))
                {
                    _queuedDest = destination;
                    destination = viaPos;
                }
            }

            if (TryApplyPath(destination)) return;

            Vector3 north = PickRoadCorridorDestination();
            if (TryApplyPath(north)) return;
        }

        public static Quaternion HeadingAlongRoad(Vector3 position)
        {
            if (IsRoute2Corridor(position)) return Quaternion.LookRotation(Route2Heading);
            return Quaternion.LookRotation(PickLongestRoadHeading(position));
        }

        public static Vector3 PickLongestRoadHeading(Vector3 position)
        {
            if (IsRoute2Corridor(position)) return Route2Heading;

            Vector3[] candidates = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            float best = 0f;
            Vector3 bestDir = Vector3.forward;

            foreach (var dir in candidates)
            {
                float walkable = MeasureWalkable(position, dir, 90f);
                if (walkable > best)
                {
                    best = walkable;
                    bestDir = dir;
                }
            }

            return bestDir;
        }

        public static float MeasureWalkable(Vector3 origin, Vector3 direction, float maxDistance)
        {
            if (!NavMesh.SamplePosition(origin, out NavMeshHit start, 8f, NavMesh.AllAreas)) return 0f;

            Vector3 flat = direction;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) return 0f;
            Vector3 end = start.position + flat.normalized * maxDistance;

            if (NavMesh.Raycast(start.position, end, out NavMeshHit blocked, NavMesh.AllAreas))
            {
                return Vector3.Distance(start.position, blocked.position);
            }

            return maxDistance;
        }

        private void Update()
        {
            if (_agent == null) return;
            if (!_agent.isOnNavMesh)
            {
                EnsureOnMesh();
                return;
            }

            if (_recoverPhase != RecoverPhase.None)
            {
                TickRecover();
                return;
            }

            _followTimer += Time.deltaTime;
            if (_followTimer >= 0.08f)
            {
                _followTimer = 0f;
                _currentTargetSpeed = CheckForwardVehicleSpeed();
                _agent.speed = _currentTargetSpeed;
                bool mustStop = _currentTargetSpeed < 0.05f;
                if (_agent.isStopped != mustStop) _agent.isStopped = mustStop;
                if (mustStop)
                {
                    _stoppedTime += 0.08f;
                    if (!_isExperimentStressVehicle && _stoppedTime >= 1.5f && ShouldRearCarRecover())
                    {
                        BeginRecover();
                    }
                }
                else
                {
                    _stoppedTime = 0f;
                }

                // #region agent log
                if (IsRoute2Corridor(transform.position) && Time.time >= _dbgWayAt)
                {
                    _dbgWayAt = Time.time + 1f;
                    Vector3 f = FlatForward();
                    float alongFwd = Vector3.Dot(f, Route2Heading);
                    Dbg(alongFwd < -0.25f ? "B" : "C", alongFwd < -0.25f ? "wrong_way" : "on_corridor",
                        "{\"name\":\"" + name +
                        "\",\"x\":" + F(transform.position.x) +
                        ",\"z\":" + F(transform.position.z) +
                        ",\"fwdZ\":" + F(f.z) +
                        ",\"along\":" + F(alongFwd) +
                        ",\"lane\":" + F(Route2LaneOffset(transform.position)) +
                        ",\"dest\":\"" + (_currentDestination != null ? _currentDestination.name : "corridor") + "\"}");
                }
                // #endregion
            }

            if (_repathCooldown > 0f) _repathCooldown -= Time.deltaTime;

            if (IsRoute2Corridor(transform.position) &&
                Vector3.Dot(FlatForward(), Route2Heading) < -0.25f &&
                _repathCooldown <= 0f)
            {
                _currentDestination = null;
                _queuedDest = null;
                _repathCooldown = 0.45f;
                AssignRoadCorridorRoute();
                return;
            }

            bool needsPath = !_agent.pathPending && (!_agent.hasPath || _agent.remainingDistance <= 8f);
            if (needsPath && _repathCooldown <= 0f)
            {
                _repathCooldown = 0.25f;
                if (_queuedDest.HasValue)
                {
                    Vector3 queued = _queuedDest.Value;
                    _queuedDest = null;
                    AssignSpawnRoute(queued);
                }
                else
                {
                    AssignRoadCorridorRoute();
                }
            }
        }

        private void BeginRecover()
        {
            _recoverPhase = RecoverPhase.Reverse;
            _recoverTime = 0f;
            _stoppedTime = 0f;
            if (_agent != null)
            {
                _agent.isStopped = false;
                _agent.updateRotation = false;
            }
        }

        private void TickRecover()
        {
            _recoverTime += Time.deltaTime;

            if (_recoverPhase == RecoverPhase.Reverse)
            {
                Vector3 back = transform.position - FlatForward() * (4.2f * Time.deltaTime);
                if (NavMesh.SamplePosition(back, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    _agent.Warp(hit.position);
                }

                if (_recoverTime >= 0.85f)
                {
                    _recoverPhase = RecoverPhase.Sidestep;
                    _recoverTime = 0f;
                    TrySidestepAndResume();
                }

                return;
            }

            FinishRecover();
        }

        private void TrySidestepAndResume()
        {
            Vector3 right = Vector3.Cross(Vector3.up, FlatForward());
            for (float lateral = 2.5f; lateral <= 5.2f; lateral += 1.3f)
            {
                Vector3 guess = transform.position + right * lateral + FlatForward() * 2.5f;
                if (!NavMesh.SamplePosition(guess, out NavMeshHit hit, 3.5f, NavMesh.AllAreas)) continue;
                if (IsSouthboundOnRoute2(transform.position, hit.position)) continue;

                _queuedDest = null;
                FinishRecover();
                AssignSpawnRoute(hit.position);
                _repathCooldown = 0.35f;
                return;
            }

            _currentDestination = null;
            _queuedDest = null;
            FinishRecover();
            AssignRoadCorridorRoute();
            _repathCooldown = 0.35f;
        }

        private void FinishRecover()
        {
            _recoverPhase = RecoverPhase.None;
            _recoverTime = 0f;
            if (_agent != null)
            {
                _agent.updateRotation = true;
                _agent.isStopped = false;
            }
        }

        private void EnsureOnMesh()
        {
            if (_agent == null) return;
            if (_agent.isOnNavMesh) return;
            if (IsRoute2Corridor(transform.position) && TrySampleRightLane(transform.position, out Vector3 right))
            {
                _agent.Warp(right);
                return;
            }
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                _agent.Warp(hit.position);
            }
        }

        private Vector3 PickRoadCorridorDestination()
        {
            Vector3 origin = transform.position;
            Vector3 forward = FlatForward();
            if (IsRoute2Corridor(origin)) forward = Route2Heading;

            float walkable = MeasureWalkable(origin, forward, 90f);
            if (walkable < 10f && !IsRoute2Corridor(origin))
            {
                forward = PickLongestRoadHeading(origin);
                walkable = MeasureWalkable(origin, forward, 90f);
            }

            float along = Mathf.Clamp(walkable * 0.85f, 16f, 70f);
            if (IsRoute2Corridor(origin) && TryWalkAlongMesh(origin, forward, along, out Vector3 walked))
                return walked;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 guess = origin + forward * along + right * rightLaneOffset;
            if (TrySampleKeepRight(guess, forward, out Vector3 sampled))
                return sampled;
            if (NavMesh.SamplePosition(origin + forward * along + right * 1.2f, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                return hit.position;
            return origin + forward * along;
        }

        private Vector3 OffsetToRightLane(Vector3 destination, Vector3 approach)
        {
            approach.y = 0f;
            if (approach.sqrMagnitude < 0.01f) return destination;
            Vector3 right = Vector3.Cross(Vector3.up, approach.normalized);
            Vector3 guess = destination + right * rightLaneOffset;
            if (TrySampleKeepRight(guess, approach, out Vector3 sampled))
                return sampled;
            return destination;
        }

        private static bool TrySampleKeepRight(Vector3 guess, Vector3 approach, out Vector3 position)
        {
            Vector3 right = Vector3.Cross(Vector3.up, approach.normalized);
            if (NavMesh.SamplePosition(guess, out NavMeshHit hit, 2.2f, NavMesh.AllAreas)
                && Vector3.Dot(hit.position - guess, right) >= -0.7f)
            {
                position = hit.position;
                return true;
            }

            position = guess;
            return false;
        }

        private bool TryApplyPath(Vector3 destination)
        {
            if (_cachedPath == null) _cachedPath = new NavMeshPath();
            if (!_agent.CalculatePath(destination, _cachedPath) ||
                _cachedPath.status == NavMeshPathStatus.PathInvalid)
            {
                // #region agent log
                Dbg("E", "path_fail",
                    "{\"name\":\"" + name +
                    "\",\"fromZ\":" + F(transform.position.z) +
                    ",\"destZ\":" + F(destination.z) +
                    ",\"destX\":" + F(destination.x) +
                    ",\"st\":\"invalid\"}");
                // #endregion
                return false;
            }

            if (PathGoesSouthOnRoute2(_cachedPath))
            {
                // #region agent log
                Dbg("E", "path_reject",
                    "{\"name\":\"" + name +
                    "\",\"fromZ\":" + F(transform.position.z) +
                    ",\"destZ\":" + F(destination.z) +
                    ",\"destX\":" + F(destination.x) +
                    ",\"lane\":" + F(Route2LaneOffset(transform.position)) +
                    ",\"dest\":\"" + (_currentDestination != null ? _currentDestination.name : "corridor") + "\"}");
                // #endregion
                return false;
            }

            _agent.SetPath(_cachedPath);
            return true;
        }

        private static bool PathGoesSouthOnRoute2(NavMeshPath path)
        {
            if (path == null || path.corners == null || path.corners.Length < 2) return false;
            for (int i = 0; i < path.corners.Length - 1; i++)
            {
                Vector3 a = path.corners[i];
                Vector3 b = path.corners[i + 1];
                if (!IsRoute2Corridor(a) && !IsRoute2Corridor(b)) continue;
                if (Vector3.Dot(b - a, Route2Heading) < -2f) return true;
            }

            return false;
        }

        private static bool IsSouthboundOnRoute2(Vector3 from, Vector3 to)
        {
            if (!IsRoute2Corridor(from) && !IsRoute2Corridor(to)) return false;
            return Vector3.Dot(to - from, Route2Heading) < -2f;
        }

        private Vector3 FlatForward()
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) return Vector3.forward;
            return forward.normalized;
        }

        private static bool IsSameDirection(Transform other, Vector3 forward)
        {
            if (other == null) return false;
            Vector3 otherForward = other.forward;
            otherForward.y = 0f;
            if (otherForward.sqrMagnitude < 0.01f) return true;
            return Vector3.Dot(otherForward.normalized, forward) >= 0.15f;
        }

        private float CheckForwardVehicleSpeed()
        {
            float speed = cruiseSpeed;
            Vector3 forward = FlatForward();

            if (!_isExperimentStressVehicle)
            {
                Transform bike = TrafficIdentity.Cyclist;
                if (bike != null)
                {
                    speed = Mathf.Min(speed, TrafficIdentity.SpeedForPointAhead(
                        transform.position, forward, bike.position, 1.7f,
                        forwardLookaheadDistance + 2f, stoppingBuffer, cruiseSpeed));
                }
            }

            if (TryFindLaneLeader(out NavMeshVehicleAI leader, out float leaderAhead))
            {
                const float holdGap = 11f;
                if (leaderAhead <= holdGap) return 0f;
                float available = Mathf.Max(0.01f, forwardLookaheadDistance - holdGap);
                speed = Mathf.Min(speed, cruiseSpeed * Mathf.Clamp01((leaderAhead - holdGap) / available));
            }

            Vector3 origin = transform.position + Vector3.up * 0.7f + transform.forward * 0.4f;
            Vector3 halfExtents = new Vector3(1.1f, 0.7f, 0.5f);
            RaycastHit[] hits = Physics.BoxCastAll(origin, halfExtents, forward, transform.rotation,
                forwardLookaheadDistance, ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null || hits[i].transform.IsChildOf(transform)) continue;

                bool cyclist = TrafficIdentity.IsCyclist(col);
                if (cyclist && _isExperimentStressVehicle) continue;
                if (!cyclist && !TrafficIdentity.IsVehicle(col)) continue;
                if (!cyclist && !IsSameDirection(col.transform.root, forward)) continue;

                if (hits[i].distance <= stoppingBuffer) return 0f;
                float available = Mathf.Max(0.01f, forwardLookaheadDistance - stoppingBuffer);
                speed = Mathf.Min(speed, cruiseSpeed * Mathf.Clamp01((hits[i].distance - stoppingBuffer) / available));
            }

            return speed;
        }

        private bool TryFindLaneLeader(out NavMeshVehicleAI leader, out float aheadDistance)
        {
            leader = null;
            aheadDistance = float.PositiveInfinity;
            Vector3 forward = FlatForward();
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            for (int i = 0; i < Active.Count; i++)
            {
                NavMeshVehicleAI other = Active[i];
                if (other == null || other == this || other._isExperimentStressVehicle) continue;

                Vector3 to = other.transform.position - transform.position;
                to.y = 0f;
                float ahead = Vector3.Dot(to, forward);
                float lateral = Mathf.Abs(Vector3.Dot(to, right));
                if (ahead < 1.5f || ahead > 28f || lateral > 2.2f) continue;
                if (Vector3.Dot(other.FlatForward(), forward) < 0.15f) continue;

                if (ahead < aheadDistance)
                {
                    aheadDistance = ahead;
                    leader = other;
                }
            }

            return leader != null;
        }

        private bool TryFindLaneTrailer(out NavMeshVehicleAI trailer)
        {
            trailer = null;
            float best = float.PositiveInfinity;
            Vector3 forward = FlatForward();
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            for (int i = 0; i < Active.Count; i++)
            {
                NavMeshVehicleAI other = Active[i];
                if (other == null || other == this) continue;

                Vector3 to = other.transform.position - transform.position;
                to.y = 0f;
                float behind = -Vector3.Dot(to, forward);
                float lateral = Mathf.Abs(Vector3.Dot(to, right));
                if (behind < 1.5f || behind > 20f || lateral > 2.2f) continue;

                if (behind < best)
                {
                    best = behind;
                    trailer = other;
                }
            }

            return trailer != null;
        }

        private bool ShouldRearCarRecover()
        {
            bool hasLeader = TryFindLaneLeader(out _, out _);
            bool hasTrailer = TryFindLaneTrailer(out _);
            if (hasLeader) return true;
            if (hasTrailer) return false;

            for (int i = 0; i < Active.Count; i++)
            {
                NavMeshVehicleAI other = Active[i];
                if (other == null || other == this) continue;
                Vector3 delta = other.transform.position - transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude > 16f * 16f) continue;
                if (other.GetInstanceID() < GetInstanceID()) return true;
                return false;
            }

            return true;
        }

        private Transform FindClaimedDestinationAhead()
        {
            if (TryFindLaneLeader(out NavMeshVehicleAI leader, out _) && leader.ClaimedDestination != null)
            {
                return leader.ClaimedDestination;
            }

            return null;
        }

        // #region agent log
        private void LogRouteAssign(string via, Vector3 dest, string destName, bool rejectedSouth)
        {
            if (!IsRoute2Corridor(transform.position) && !IsRoute2Corridor(dest)) return;
            Dbg("D", "assign",
                "{\"name\":\"" + name +
                "\",\"via\":\"" + via +
                "\",\"fromZ\":" + F(transform.position.z) +
                ",\"destZ\":" + F(dest.z) +
                ",\"dZ\":" + F(dest.z - transform.position.z) +
                ",\"fwdZ\":" + F(FlatForward().z) +
                ",\"lane\":" + F(Route2LaneOffset(transform.position)) +
                ",\"dest\":\"" + destName +
                "\",\"rejS\":" + (rejectedSouth ? "true" : "false") +
                ",\"fromIn\":" + (IsRoute2Corridor(transform.position) ? "true" : "false") +
                ",\"destIn\":" + (IsRoute2Corridor(dest) ? "true" : "false") + "}");
        }

        private static void Dbg(string hid, string msg, string data)
        {
            try
            {
                long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                File.AppendAllText("/Users/admin/Documents/GitHub/Sumonity-UnityBaseProject/.cursor/debug-051389.log",
                    "{\"sessionId\":\"051389\",\"hypothesisId\":\"" + hid +
                    "\",\"location\":\"NavMeshVehicleAI.cs\",\"message\":\"" + msg +
                    "\",\"data\":" + data + ",\"timestamp\":" + ts + "}\n");
            }
            catch
            {
            }
        }

        private static string F(float v)
        {
            return v.ToString("F1", CultureInfo.InvariantCulture);
        }
        // #endregion
    }
}
