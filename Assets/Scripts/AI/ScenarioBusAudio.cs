using UnityEngine;

namespace CyclingExperiment.AI
{
    /// <summary>
    /// 3D bus sounds for the Route 1 overtake: departure, engine loop, brake, then idle at the stop.
    /// </summary>
    public class ScenarioBusAudio : MonoBehaviour
    {
        [SerializeField] private AudioClip engineLoop;
        [SerializeField] private AudioClip brake;
        [SerializeField] private AudioClip idleStop;
        [SerializeField] private AudioClip departure;

        private WaypointFollower _follower;
        private AudioSource _loopSource;
        private AudioSource _oneShotSource;
        private bool _parked;

        public void Bind(WaypointFollower follower, AudioClip engine, AudioClip brakeClip, AudioClip idle, AudioClip depart)
        {
            _follower = follower;
            engineLoop = engine;
            brake = brakeClip;
            idleStop = idle;
            departure = depart;
            EnsureSources();
        }

        private void Awake()
        {
            EnsureSources();
        }

        private void Start()
        {
            if (_follower == null) _follower = GetComponent<WaypointFollower>();
            EnsureSources();

            if (departure != null)
            {
                _oneShotSource.PlayOneShot(departure, 0.85f);
            }

            PlayLoop(engineLoop, 0.55f);
        }

        private void Update()
        {
            if (_parked || _follower == null || !_follower.IsAtEnd) return;
            _parked = true;
            StartCoroutine(ParkAndIdle());
        }

        private System.Collections.IEnumerator ParkAndIdle()
        {
            if (_loopSource != null && _loopSource.isPlaying)
            {
                float startVolume = _loopSource.volume;
                float elapsed = 0f;
                const float fade = 0.45f;
                while (elapsed < fade && _loopSource != null)
                {
                    elapsed += Time.deltaTime;
                    _loopSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fade);
                    yield return null;
                }
            }

            if (brake != null && _oneShotSource != null)
            {
                _oneShotSource.PlayOneShot(brake, 1f);
            }

            PlayLoop(idleStop, 0.4f);
        }

        private void EnsureSources()
        {
            if (_loopSource == null)
            {
                _loopSource = gameObject.AddComponent<AudioSource>();
                ConfigureSpatial(_loopSource);
                _loopSource.loop = true;
                _loopSource.playOnAwake = false;
            }

            if (_oneShotSource == null)
            {
                _oneShotSource = gameObject.AddComponent<AudioSource>();
                ConfigureSpatial(_oneShotSource);
                _oneShotSource.loop = false;
                _oneShotSource.playOnAwake = false;
            }
        }

        private void PlayLoop(AudioClip clip, float volume)
        {
            if (_loopSource == null) return;
            _loopSource.Stop();
            if (clip == null) return;
            _loopSource.clip = clip;
            _loopSource.volume = volume;
            _loopSource.Play();
        }

        private static void ConfigureSpatial(AudioSource source)
        {
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.minDistance = 4f;
            source.maxDistance = 28f;
            source.rolloffMode = AudioRolloffMode.Linear;
        }
    }
}
