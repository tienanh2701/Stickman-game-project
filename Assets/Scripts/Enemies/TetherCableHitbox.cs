using UnityEngine;

namespace StickmanBeatEmUp.Enemies
{
    /// <summary>
    /// Non-destructible visual anchor on the tether connecting War Hammer Titan to CrystalCore.
    /// Intentionally does NOT implement IDamageable (preserving LSP) because its collider stays disabled
    /// and the tether can only be broken by destroying the 500 HP CrystalCore.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class TetherCableHitbox : MonoBehaviour
    {
        [SerializeField] private float cableHealth = 20f;
        [SerializeField] private SpriteRenderer indicatorRenderer;

        public Transform Transform => transform;
        public float CableHealth => cableHealth;

        private Collider2D cableCollider;

        private void Awake()
        {
            cableCollider = GetComponent<Collider2D>();
            if (cableCollider != null) cableCollider.enabled = false;
            if (indicatorRenderer != null) indicatorRenderer.enabled = false;
        }

        private void Start()
        {
            if (cableCollider != null) cableCollider.enabled = false;
        }

        public void SetCableHealth(float hp)
        {
            cableHealth = Mathf.Max(1f, hp);
        }
    }
}
