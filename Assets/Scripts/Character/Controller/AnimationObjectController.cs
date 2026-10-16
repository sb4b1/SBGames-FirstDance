using System.Collections.Generic;
using UnityEngine;
using SB4b1.Logging;

[RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
public class AnimationObjectController : MonoBehaviour
{
    [Header("Core Components")]
    [SerializeField] protected Animator animator;
    [SerializeField] protected SpriteRenderer sprite;

    [Header("Sorting Configuration")]
    [SerializeField] protected bool enableYSorting = true;
    [SerializeField] protected string targetSortingLayer = "Character";
    [SerializeField] protected float sortingOrdersPerWorldUnit = 100f;

    protected static readonly int HASH_GET_HIT = Animator.StringToHash("GetHit");

    private readonly HashSet<int> parameterHashes = new();
    protected bool hasGetHitParam;

    private float cachedCastDuration = -1f;
    private bool cachedHasCastCompleteEvent = false;

    public float CastAnimationDuration
    {
        get
        {
            if (cachedCastDuration < 0f)
            {
                CacheCastAnimationInfo();
            }
            return cachedCastDuration;
        }
    }

    public bool HasCastCompleteEvent
    {
        get
        {
            if (cachedCastDuration < 0f)
            {
                CacheCastAnimationInfo();
            }
            return cachedHasCastCompleteEvent;
        }
    }

    public Animator Animator => animator;
    public SpriteRenderer Sprite => sprite;

    protected virtual void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (sprite == null) sprite = GetComponent<SpriteRenderer>();

        CacheAnimatorParameters();
        CacheCastAnimationInfo();
    }

    public virtual void CacheAnimatorParameters()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
        }
        if (animator == null) return;

        parameterHashes.Clear();
        hasGetHitParam = false;

        foreach (var parameter in animator.parameters)
        {
            parameterHashes.Add(parameter.nameHash);
            if (parameter.nameHash == HASH_GET_HIT)
            {
                hasGetHitParam = true;
            }
        }
    }

    public void CacheCastAnimationInfo()
    {
        cachedCastDuration = 0f;
        cachedHasCastCompleteEvent = false;

        if (animator == null)
        {
            animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
        }
        if (animator == null || animator.runtimeAnimatorController == null) return;

        var clips = animator.runtimeAnimatorController.animationClips;
        if (clips == null) return;

        foreach (var clip in clips)
        {
            if (clip != null && clip.name.IndexOf("Cast", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                cachedCastDuration = clip.length;
                var events = clip.events;
                if (events != null)
                {
                    for (int i = 0; i < events.Length; i++)
                    {
                        string fn = events[i].functionName;
                        if (fn == "AnimationEvent_CastSkillComplete" || fn == "OnCastAnimationEnd")
                        {
                            cachedHasCastCompleteEvent = true;
                            break;
                        }
                    }
                }
                break;
            }
        }
    }

    public bool HasParameter(int hash) => parameterHashes.Contains(hash);
    public bool HasParameter(string paramName) => parameterHashes.Contains(Animator.StringToHash(paramName));

    protected virtual void LateUpdate()
    {
        UpdateSortingOrder();
    }

    protected virtual void UpdateSortingOrder()
    {
        if (!enableYSorting || sprite == null) return;
        if (!string.IsNullOrEmpty(targetSortingLayer) && sprite.sortingLayerName != targetSortingLayer) return;

        Transform positionSource = GetSortingPositionSource();
        sprite.sortingOrder = Mathf.RoundToInt(-positionSource.position.y * sortingOrdersPerWorldUnit);
    }

    protected virtual Transform GetSortingPositionSource()
    {
        return transform.parent != null ? transform.parent : transform;
    }

    // =========================
    // Playback API
    // =========================

    public virtual void PlayHit()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
        }
        if (animator == null) return;

        if (parameterHashes.Count == 0)
        {
            CacheAnimatorParameters();
        }

        if (hasGetHitParam)
        {
            animator.SetTrigger(HASH_GET_HIT);
        }
    }

    public virtual void PlayTrigger(string triggerName)
    {
        if (animator == null || string.IsNullOrEmpty(triggerName)) return;
        animator.SetTrigger(triggerName);
    }

    public virtual void PlayTrigger(int triggerHash)
    {
        if (animator == null) return;
        animator.SetTrigger(triggerHash);
    }

    public virtual void PlayState(string stateName, int layer = 0, float normalizedTime = 0f)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return;
        animator.Play(stateName, layer, normalizedTime);
    }

    public virtual void PlayState(int stateHash, int layer = 0, float normalizedTime = 0f)
    {
        if (animator == null) return;
        animator.Play(stateHash, layer, normalizedTime);
    }

    public virtual void SetBool(string paramName, bool value)
    {
        if (animator == null || string.IsNullOrEmpty(paramName)) return;
        if (parameterHashes.Count == 0) CacheAnimatorParameters();
        if (!HasParameter(paramName)) return;
        animator.SetBool(paramName, value);
    }

    public virtual void SetBool(int paramHash, bool value)
    {
        if (animator == null) return;
        if (parameterHashes.Count == 0) CacheAnimatorParameters();
        if (!HasParameter(paramHash)) return;
        animator.SetBool(paramHash, value);
    }

    public virtual void SetFloat(string paramName, float value)
    {
        if (animator == null || string.IsNullOrEmpty(paramName)) return;
        if (parameterHashes.Count == 0) CacheAnimatorParameters();
        if (!HasParameter(paramName)) return;
        animator.SetFloat(paramName, value);
    }

    public virtual void SetFloat(int paramHash, float value)
    {
        if (animator == null) return;
        if (parameterHashes.Count == 0) CacheAnimatorParameters();
        if (!HasParameter(paramHash)) return;
        animator.SetFloat(paramHash, value);
    }

    public virtual void UpdateAttack(bool isAttacking) { }
    public virtual void UpdateCastSkill(bool isCasting) { }
}
