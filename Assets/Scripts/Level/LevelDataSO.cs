using UnityEngine;

namespace StickmanBeatEmUp.Level
{
    /// <summary>
    /// Extended World/Stage ScriptableObject pairing StageDataSO waves/bosses with World background & difficulty scaling.
    /// </summary>
    [CreateAssetMenu(fileName = "NewLevelData", menuName = "Stickman BeatEmUp/Level Data SO")]
    public class LevelDataSO : StageDataSO
    {
        [Header("World Visuals & Scaling")]
        public Sprite backgroundSprite;
        public Color groundColor = new Color(0.18f, 0.20f, 0.24f, 1f);
        public float enemyHpMultiplier = 1f;
        public float enemyDamageMultiplier = 1f;

        [Header("Boss Fight Reinforcements")]
        public GameObject supportMeleeCreepPrefab;
        public GameObject supportRangedCreepPrefab;
        public GameObject supportElitePrefab;
    }
}
