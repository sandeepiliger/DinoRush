using System.Collections.Generic;
using DinoRush.Core;
using UnityEngine;

namespace DinoRush.Runtime
{
    // Same pooling contract as ObstaclePool (section 34). Coins churn faster than obstacles —
    // a single Safe segment places three and a CoinPattern five — so this is the pool that
    // would actually show up in a GC trace if it were built with Instantiate/Destroy.
    public sealed class CoinPool
    {
        private readonly Transform _parent;
        private readonly float _radius;
        private readonly Stack<GameObject> _idle = new Stack<GameObject>();
        private int _created;

        public CoinPool(Transform parent, float radiusMeters, int prewarmCount)
        {
            _parent = parent;
            _radius = radiusMeters;

            for (int i = 0; i < prewarmCount; i++)
                _idle.Push(CreateInstance());
        }

        public int TotalCreated => _created;

        public GameObject Rent(CoinSpawn coin)
        {
            var instance = _idle.Count > 0 ? _idle.Pop() : CreateInstance();
            instance.transform.position = new Vector3(coin.DistanceMeters, coin.HeightMeters, 0f);
            instance.SetActive(true);
            return instance;
        }

        public void Return(GameObject instance)
        {
            instance.SetActive(false);
            _idle.Push(instance);
        }

        private GameObject CreateInstance()
        {
            var instance = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            instance.name = "Coin";
            instance.transform.SetParent(_parent, worldPositionStays: false);
            // A flattened cylinder turned on its side reads as a coin from a side-on camera.
            instance.transform.localScale = new Vector3(_radius * 2f, 0.06f, _radius * 2f);
            instance.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Object.Destroy(instance.GetComponent<Collider>());

            var renderer = instance.GetComponent<Renderer>();
            renderer.material.color = new Color(0.98f, 0.78f, 0.20f);

            instance.SetActive(false);
            _created++;
            return instance;
        }
    }
}
