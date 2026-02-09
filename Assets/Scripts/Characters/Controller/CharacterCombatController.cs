using UnityEngine;

[RequireComponent(typeof(CharacterBrain))]
public class CharacterCombatController : MonoBehaviour
{
    [SerializeField]private CharacterBrain brain;

    private void Awake()
    {
        if (brain == null) brain = GetComponent<CharacterBrain>();
    } 

    public void Attack()
    {
        brain.TryStartAttack();
    }
}