using UnityEngine;

public class AnimationEventController : MonoBehaviour
{
    [SerializeField] private CharacterCombatController combatController;
    [SerializeField] private CharacterSkillController skillController;
    [SerializeField] private CharacterBrain brain;

    private void Awake()
    {
        if (combatController == null)
            combatController = GetComponentInParent<CharacterCombatController>();

        if (skillController == null)
        {
            skillController = GetComponentInParent<CharacterSkillController>();
            if (skillController == null && transform.root != null)
                skillController = transform.root.GetComponentInChildren<CharacterSkillController>();
        }

        if (brain == null)
        {
            brain = GetComponentInParent<CharacterBrain>();
            if (brain == null && transform.root != null)
                brain = transform.root.GetComponentInChildren<CharacterBrain>();
        }
    }

    public void AnimationEvent_Attack()
    {
        combatController?.SearchForTargets();
    }

    public void AnimationEvent_CastSkill()
    {
        skillController?.ExecutePendingCast();
    }

    public void AnimationEvent_CastSkillComplete()
    {
        brain?.EndCastSkill();
    }

    public void AnimationEvent_AttackComplete()
    {
        brain?.EndAttack();
    }
}