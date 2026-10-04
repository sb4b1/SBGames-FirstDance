using UnityEngine;

public class AnimationEventController : MonoBehaviour
{
    [SerializeField] private CharacterCombatController combatController;
    [SerializeField] private CharacterSkillController skillController;

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
    }

    public void AnimationEvent_Attack()
    {
        combatController.SearchForTargets();
    }

    public void AnimationEvent_CastSkill()
    {
        if (skillController != null)
        {
            skillController.ExecutePendingCast();
        }
    }
}