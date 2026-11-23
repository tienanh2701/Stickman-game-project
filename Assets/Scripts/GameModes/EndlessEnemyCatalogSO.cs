using System.Collections.Generic;
using UnityEngine;

namespace StickmanBeatEmUp.GameModes
{
    /// <summary>
    /// ScriptableObject catalog holding 3-World Endless Mode enemy prefabs,
    /// eliminating hardcoded "Assets/Prefabs/..." asset path strings from EndlessModeManager.
    /// </summary>
    [CreateAssetMenu(fileName = "EndlessEnemyCatalog", menuName = "Stickman BeatEmUp/Endless Enemy Catalog SO")]
    public class EndlessEnemyCatalogSO : ScriptableObject
    {
        [Header("3-World Canonical Endless Pools")]
        public List<GameObject> meleeCreeps = new List<GameObject>();
        public List<GameObject> rangedCreeps = new List<GameObject>();
        public List<GameObject> elites = new List<GameObject>();
        public List<GameObject> bosses = new List<GameObject>();

        public bool HasAnyPrefabs =>
            (meleeCreeps != null && meleeCreeps.Count > 0) ||
            (rangedCreeps != null && rangedCreeps.Count > 0) ||
            (elites != null && elites.Count > 0) ||
            (bosses != null && bosses.Count > 0);
    }
}
