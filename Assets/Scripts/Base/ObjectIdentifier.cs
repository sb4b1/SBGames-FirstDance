using UnityEngine;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, null, "ObjectIdenifier")]
public abstract class ObjectIdentifier : MonoBehaviour, IIdentifier
{
    [SerializeField] protected string uniqueId;
    public string UniqueId => uniqueId;

    public virtual void GenerateID()
    {
        uniqueId = System.Guid.NewGuid().ToString();
    }

    protected virtual void Awake()
    {
        if (string.IsNullOrEmpty(uniqueId))
        {
            GenerateID();
        }
    }
}