using UnityEngine;

public interface ISkillMovement
{
    void Initialize(SkillInstance skill, Vector3 direction);
    void Tick(SkillInstance skill, float dt);
    void Redirect(SkillInstance skill, GameObject newTarget);
}
