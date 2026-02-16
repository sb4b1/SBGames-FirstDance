using System.Collections.Generic;
using ArtificeToolkit.Attributes;
using UnityEngine;

public class CharacterSkillController : MonoBehaviour
{
    public List<Skill> skills;

    [System.Serializable]
    public class Skill
    {
        [SerializeField] private SkillCaster skill;

        [Button]
        public void Trigger()
        {
            skill.TryCastSkill();
        }
    }

}