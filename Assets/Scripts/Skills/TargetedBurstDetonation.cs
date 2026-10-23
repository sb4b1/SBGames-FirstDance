using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Detonation and lifecycle controller for targeted single-target burst effects (e.g. instant single-target heal).
/// Follows the chosen target during wind-up, re-validates targeting rules at burst moment,
/// applies the skill hit/heal directly to that exact target once, and returns the effect prefab to the pool cleanly.
/// Fizzles immediately with no heal or error if target dies or despawns during wind-up.
/// </summary>
public class TargetedBurstDetonation : MonoBehaviour, ISkillDetonation, IPoolable
{
    [SerializeField] private float windupDuration = 0.5f;
    [SerializeField] private VisualAnchor visualAnchor = VisualAnchor.Feet;
    [SerializeField] private Vector3 feetOffset = Vector3.zero;
    [SerializeField] private Vector3 bodyCenterOffset = new Vector3(0f, 0.5f, 0f);

    [Header("Target Visual Fitting")]
    [SerializeField] private bool fitTargetVisuals;
    [SerializeField] private Sprite fitReferenceSprite;
    [SerializeField, Min(0.01f)] private float targetCoverage = 1.1f;
    [Tooltip("Ground contact point within the effect sprite canvas, measured from its bottom left.")]
    [SerializeField] private Vector2 groundAnchorNormalized = new Vector2(0.5f, 0.203125f);
    [Tooltip("Diameter of the effect's main ground ring as a fraction of its sprite canvas width.")]
    [SerializeField, Range(0.01f, 1f)] private float groundRingWidthFraction = 0.65f;

    private struct VisualPart
    {
        public SpriteRenderer Renderer;
        public Bounds LocalBounds;
        public Vector3 LocalFeet;
    }

    private readonly List<SpriteRenderer> rendererBuffer = new List<SpriteRenderer>(16);
    private readonly List<Vector2> outlineBuffer = new List<Vector2>(64);
    private readonly List<VisualPart> visualParts = new List<VisualPart>(16);
    private Vector3 authoredScale;
    private bool hasAuthoredScale;
    private Sprite referenceSprite;

    private SkillInstance currentSkill;
    private GameObject boundTarget;
    private CharacterStatsManager boundTargetStats;
    private TargetedBurstTargetLifetime targetLifetime;
    private uint targetGeneration;

    private bool IsTargetValid() => boundTarget != null && boundTarget.activeInHierarchy &&
        (boundTargetStats == null || !boundTargetStats.IsDead) && targetLifetime != null && targetLifetime.Generation == targetGeneration;

    private void LateUpdate()
    {
        if (currentSkill == null) return;
        if (!IsTargetValid()) { DespawnEffect(); return; }
        UpdateVisualPlacement();
    }
    private Coroutine burstCoroutine;
    private bool hasBurst;

    public float WindupDuration
    {
        get => windupDuration;
        set => windupDuration = value;
    }

    public VisualAnchor Anchor
    {
        get => visualAnchor;
        set => visualAnchor = value;
    }

    public bool HasBurst => hasBurst;

    private void CacheTargetVisuals()
    {
        if (!hasAuthoredScale)
        {
            authoredScale = transform.localScale;
            hasAuthoredScale = true;
        }
        transform.localScale = authoredScale;
        visualParts.Clear();
        referenceSprite = fitReferenceSprite;
        if (referenceSprite == null && TryGetComponent<SpriteRenderer>(out var effectRenderer))
            referenceSprite = effectRenderer.sprite;
        if (!fitTargetVisuals || referenceSprite == null) return;

        boundTarget.GetComponentsInChildren(true, rendererBuffer);
        for (int i = 0; i < rendererBuffer.Count; i++)
        {
            var renderer = rendererBuffer[i];
            if (renderer == null || renderer.sprite == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            string layer = renderer.sortingLayerName;
            if (layer == "SkillFX" || layer == "UI" || layer == "Ground") continue;
            if (renderer.GetComponentInParent<SkillInstance>() != null) continue;
            // Cache one pose per cast so animation frames do not make the aura pulse in size.
            var sprite = renderer.sprite;
            Bounds bounds = sprite.bounds;
            bool hasOutline = false;
            for (int shape = 0; shape < sprite.GetPhysicsShapeCount(); shape++)
            {
                sprite.GetPhysicsShape(shape, outlineBuffer);
                for (int point = 0; point < outlineBuffer.Count; point++)
                {
                    Vector3 position = outlineBuffer[point];
                    if (!hasOutline) { bounds = new Bounds(position, Vector3.zero); hasOutline = true; }
                    else bounds.Encapsulate(position);
                }
            }
            // The lower silhouette locates the feet without letting a staff or weapon pull the aura sideways.
            float footMinX = float.PositiveInfinity, footMaxX = float.NegativeInfinity;
            float footBandTop = bounds.min.y + bounds.size.y * 0.15f;
            for (int shape = 0; shape < sprite.GetPhysicsShapeCount(); shape++)
            {
                sprite.GetPhysicsShape(shape, outlineBuffer);
                for (int point = 0; point < outlineBuffer.Count; point++)
                {
                    Vector2 position = outlineBuffer[point];
                    if (position.y > footBandTop) continue;
                    footMinX = Mathf.Min(footMinX, position.x);
                    footMaxX = Mathf.Max(footMaxX, position.x);
                }
            }
            float feetX = float.IsPositiveInfinity(footMinX) ? bounds.center.x : (footMinX + footMaxX) * 0.5f;
            visualParts.Add(new VisualPart { Renderer = renderer, LocalBounds = bounds,
                LocalFeet = new Vector3(feetX, bounds.min.y, bounds.center.z) });
        }
        rendererBuffer.Clear();
    }

    private bool TryGetVisualBounds(out Bounds bounds, out Vector3 feet)
    {
        bounds = default;
        feet = default;
        bool found = false;
        for (int i = 0; i < visualParts.Count; i++)
        {
            var part = visualParts[i];
            var renderer = part.Renderer;
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            Vector3 center = part.LocalBounds.center;
            if (renderer.flipX) center.x = -center.x;
            if (renderer.flipY) center.y = -center.y;
            var matrix = renderer.transform.localToWorldMatrix;
            Vector3 extents = part.LocalBounds.extents;
            Vector3 x = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
            Vector3 y = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
            Vector3 z = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));
            var world = new Bounds(matrix.MultiplyPoint3x4(center), new Vector3(
                Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z)) * 2f);
            Vector3 localFeet = part.LocalFeet;
            if (renderer.flipX) localFeet.x = -localFeet.x;
            if (renderer.flipY) localFeet.y = -localFeet.y;
            Vector3 worldFeet = matrix.MultiplyPoint3x4(localFeet);
            if (!found || worldFeet.y < feet.y) feet = worldFeet;
            if (!found) { bounds = world; found = true; }
            else bounds.Encapsulate(world);
        }
        return found;
    }

    private void UpdateVisualPlacement()
    {
        if (fitTargetVisuals && referenceSprite != null && TryGetVisualBounds(out var bounds, out var feet))
        {
            Vector2 canvas = referenceSprite.rect.size / referenceSprite.pixelsPerUnit;
            float ringWidth = Mathf.Max(0.001f, canvas.x * groundRingWidthFraction);
            float heightAboveGround = Mathf.Max(0.001f, canvas.y * (1f - groundAnchorNormalized.y));
            float scale = Mathf.Max(bounds.size.x / ringWidth, bounds.size.y / heightAboveGround) * targetCoverage;
            transform.localScale = new Vector3(scale, scale, 1f);
            Vector3 anchor = visualAnchor == VisualAnchor.BodyCenter ? bounds.center :
                feet;
            Vector3 spriteGround = new Vector3(
                (referenceSprite.rect.width * groundAnchorNormalized.x - referenceSprite.pivot.x) / referenceSprite.pixelsPerUnit,
                (referenceSprite.rect.height * groundAnchorNormalized.y - referenceSprite.pivot.y) / referenceSprite.pixelsPerUnit, 0f);
            Vector3 offset = visualAnchor == VisualAnchor.BodyCenter ? bodyCenterOffset : feetOffset;
            transform.position = anchor + offset - Vector3.Scale(spriteGround, transform.localScale);
            return;
        }
        if (hasAuthoredScale) transform.localScale = authoredScale;
        transform.position = boundTarget.transform.position + (visualAnchor == VisualAnchor.BodyCenter ? bodyCenterOffset : feetOffset);
    }

    public void TriggerBurst()
    {
        if (burstCoroutine != null)
        {
            StopCoroutine(burstCoroutine);
            burstCoroutine = null;
        }

        if (!IsTargetValid())
        {
            DespawnEffect();
            return;
        }

        if (currentSkill != null && currentSkill.CanHitTarget(boundTarget, HitContext.Direct))
        {
            hasBurst = true;
            currentSkill.OnHit(boundTarget, HitContext.Direct);
        }

        DespawnEffect();
    }

    public void Detonate(SkillInstance skill)
    {
        currentSkill = skill;
        if (burstCoroutine != null)
        {
            StopCoroutine(burstCoroutine);
            burstCoroutine = null;
        }

        if (skill != null && skill.Data != null)
        {
            if (skill.Data.windupDuration > 0f)
            {
                windupDuration = skill.Data.windupDuration;
            }
            visualAnchor = skill.Data.visualAnchor;
        }

        boundTarget = skill != null ? skill.currentTarget : null;
        if (boundTarget == null && skill != null && skill.CasterGameObject != null && skill.Data != null && skill.Data.selfTarget == SkillData.SelfTarget.Only)
        {
            boundTarget = skill.CasterGameObject;
        }

        if (boundTarget == null)
        {
            // No target to attach to, fizzle cleanly
            DespawnEffect();
            return;
        }

        boundTargetStats = boundTarget.GetComponent<CharacterStatsManager>() ?? boundTarget.GetComponentInParent<CharacterStatsManager>();
        if (boundTargetStats != null && boundTargetStats.IsDead)
        {
            DespawnEffect();
            return;
        }

        // Keep the effect independent of target activation. Lifetime generations reject pool reuse.
        targetLifetime = boundTarget.GetComponent<TargetedBurstTargetLifetime>();
        if (targetLifetime == null) targetLifetime = boundTarget.AddComponent<TargetedBurstTargetLifetime>();
        targetGeneration = targetLifetime.Generation;
        transform.SetParent(null);
        transform.rotation = Quaternion.identity;
        CacheTargetVisuals();
        UpdateVisualPlacement();

        var animator = GetComponent<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.Play(0, 0, 0f);
            animator.Update(0f);
        }
        hasBurst = false;
        burstCoroutine = StartCoroutine(WindupRoutine());
    }

    private IEnumerator WindupRoutine()
    {
        float timer = 0f;
        while (timer < windupDuration)
        {
            // Fizzle check during windup: target destroyed, inactive, or died
            if (!IsTargetValid())
            {
                DespawnEffect();
                yield break;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        // Burst Moment: Re-validate target against TargetFilter and SelfTarget
        if (!IsTargetValid())
        {
            DespawnEffect();
            yield break;
        }

        if (currentSkill != null && currentSkill.CanHitTarget(boundTarget, HitContext.Direct))
        {
            hasBurst = true;
            currentSkill.OnHit(boundTarget, HitContext.Direct);
        }

        // Wait brief frame / let animation finish if needed, or despawn
        DespawnEffect();
    }

    private void DespawnEffect()
    {
        if (burstCoroutine != null)
        {
            StopCoroutine(burstCoroutine);
            burstCoroutine = null;
        }

        var skill = currentSkill;
        currentSkill = null;
        boundTarget = null;
        boundTargetStats = null;
        targetLifetime = null;
        visualParts.Clear();
        referenceSprite = null;
        if (hasAuthoredScale) transform.localScale = authoredScale;
        transform.SetParent(null);
        if (skill != null)
        {
            skill.OnDespawn();
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
        else if (ObjectPoolManager.Instance != null && gameObject.activeSelf && TryGetComponent<PooledObject>(out _))
        {
            ObjectPoolManager.Instance.Despawn(gameObject);
        }
        else if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    public void OnSpawn()
    {
        hasBurst = false;
        boundTarget = null;
        boundTargetStats = null;
        targetLifetime = null;
        visualParts.Clear();
        referenceSprite = null;
        if (hasAuthoredScale) transform.localScale = authoredScale;
    }

    public void OnDespawn()
    {
        if (burstCoroutine != null)
        {
            StopCoroutine(burstCoroutine);
            burstCoroutine = null;
        }
        transform.SetParent(null);
        boundTarget = null;
        boundTargetStats = null;
        targetLifetime = null;
        visualParts.Clear();
        referenceSprite = null;
        if (hasAuthoredScale) transform.localScale = authoredScale;
        currentSkill = null;
        hasBurst = false;
    }

    private void OnDisable()
    {
        if (burstCoroutine != null)
        {
            StopCoroutine(burstCoroutine);
            burstCoroutine = null;
        }
    }
}
