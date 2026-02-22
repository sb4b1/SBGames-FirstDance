using System.Collections.Generic;
using ArtificeToolkit.Attributes;
using UnityEngine;

public class CharacterSkillController : MonoBehaviour
{
    public List<Skill> skills;
    [SerializeField] private GameObject skillSpawnPoint;
    public GameObject SpawnPoint => skillSpawnPoint;

    private void Start()
    {
        if (skillSpawnPoint == null)
        {
            skillSpawnPoint = transform.Find("SpawnPoint").gameObject;
        }
    }

    [System.Serializable]
    public class Skill
    {
        [SerializeField] private SkillCaster skill;

        [Button("Trigger Skill")]
        public void Trigger()
        {
            skill.TryCastSkill();
        }
    }

}