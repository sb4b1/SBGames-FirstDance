using UnityEngine;

public class BeamMovement : MonoBehaviour, ISkillMovement
{
    public void Initialize(SkillInstance skill, Vector3 dir) {}
    public void Tick(SkillInstance skill, float dt) {}
    public void Redirect(SkillInstance skill, GameObject target) {}
}
