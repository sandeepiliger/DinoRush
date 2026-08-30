using System.Collections.Generic;
using DinoRush.Core;
using UnityEngine;

namespace DinoRush.Runtime
{
    // Pooled roadside markers, recycled ahead of the player as they scroll past.
    //
    // This exists for a gameplay reason, not decoration: over featureless ground the camera
    // tracks the player exactly, so nothing on screen moves and the run reads as stationary no
    // matter how fast it is. Section 16 escalates speed continuously, and the player cannot
    // feel an escalation they cannot see. These give the eye something to measure motion
    // against. Real biome props (section 6) replace them in M6.
    public sealed class SceneryStrip
    {
        private const float SpacingMeters = 9f;
        private const float AheadMeters = 90f;
        private const float BehindMeters = 20f;

        private readonly Transform _parent;
        private readonly Queue<GameObject> _active = new Queue<GameObject>();
        private readonly Stack<GameObject> _idle = new Stack<GameObject>();
        private readonly SeededRandom _random = new SeededRandom(20260830);

        private float _nextMarkerDistance;

        public SceneryStrip(Transform parent, int prewarmCount)
        {
            _parent = parent;
            for (int i = 0; i < prewarmCount; i++)
                _idle.Push(CreateInstance());
        }

        public void Reset()
        {
            while (_active.Count > 0) _idle.Push(Deactivate(_active.Dequeue()));
            _nextMarkerDistance = 0f;
        }

        public void Sync(float playerDistance, BiomePalette palette)
        {
            while (_nextMarkerDistance < playerDistance + AheadMeters)
            {
                Place(_nextMarkerDistance, palette);
                _nextMarkerDistance += SpacingMeters;
            }

            while (_active.Count > 0 && _active.Peek().transform.position.x < playerDistance - BehindMeters)
                _idle.Push(Deactivate(_active.Dequeue()));
        }

        private void Place(float distance, BiomePalette palette)
        {
            var instance = _idle.Count > 0 ? _idle.Pop() : CreateInstance();

            // Alternating sides, with a seeded jitter so the strip doesn't read as a metronome.
            float side = _active.Count % 2 == 0 ? 5.5f : -5.5f;
            float jitter = (float)(_random.NextDouble() * 1.6 - 0.8);
            float height = 1.2f + (float)(_random.NextDouble() * 1.8);

            instance.transform.localScale = new Vector3(0.5f, height, 0.5f);
            instance.transform.position = new Vector3(distance + jitter, height * 0.5f, side);
            // Tinted at spawn, like obstacles — see ObstaclePool.Rent for why.
            instance.GetComponent<Renderer>().material.color = palette.Scenery.ToColor();
            instance.SetActive(true);
            _active.Enqueue(instance);
        }

        private static GameObject Deactivate(GameObject instance)
        {
            instance.SetActive(false);
            return instance;
        }

        private GameObject CreateInstance()
        {
            var instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            instance.name = "SceneryMarker";
            instance.transform.SetParent(_parent, worldPositionStays: false);
            Object.Destroy(instance.GetComponent<Collider>());
            instance.GetComponent<Renderer>().material.color = new Color(0.22f, 0.30f, 0.16f);
            instance.SetActive(false);
            return instance;
        }
    }
}
