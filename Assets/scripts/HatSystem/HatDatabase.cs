using System.Collections.Generic;
using UnityEngine;

namespace Kotenkoff
{
    [CreateAssetMenu(fileName = "HatDatabase", menuName = "Kotenkoff/Hat Database")]
    public class HatDatabase : ScriptableObject
    {
        [System.Serializable]
        public struct HatData
        {
            public int id;
            public GameObject prefab;
            public string hatName;
        }

        [SerializeField] private List<HatData> hats = new List<HatData>();


        public GameObject GetHatPrefab(int id)
        {
            foreach (var hat in hats)
            {
                if (hat.id == id) return hat.prefab;
            }
            return null;
        }
    }
}