using UnityEngine;

public class AnimationEventController : MonoBehaviour
{
    [SerializeField] private CharacterCombatController combatController;

    private void Awake()
    {
        if (combatController == null)
            combatController = GetComponentInParent<CharacterCombatController>();
    }

    public void AnimationEvent_Attack()
    {
        combatController.SearchForTargets();
    }
}