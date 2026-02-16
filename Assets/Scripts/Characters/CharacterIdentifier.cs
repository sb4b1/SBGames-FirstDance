using UnityEngine;

public class CharacterIdentifier : MonoBehaviour, IIdentifier
{
    [SerializeField] private string id;
    public string ID => id;

    public void GenerateID()
    {
        id = System.Guid.NewGuid().ToString();
    }

    private void Awake()
    {
        if (string.IsNullOrEmpty(id))
        {
            GenerateID();
        }
    }
}