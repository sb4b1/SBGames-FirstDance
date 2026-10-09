using UnityEngine;

[DisallowMultipleComponent]
public class SquadMember : MonoBehaviour, ISquadMember
{
    [SerializeField] private PartyLeader currentLeader;

    private CharacterStatsManager statsManager;
    private BaseAI baseAI;

    public GameObject GameObject => gameObject;
    public PartyLeader Leader => currentLeader;
    public bool HasLeader => currentLeader != null && currentLeader.IsAlive;
    public bool IsAlive => statsManager == null || statsManager.Life > 0;

    public SquadOrderType CurrentOrder => HasLeader ? currentLeader.Blackboard.CurrentOrder : SquadOrderType.None;
    public Transform FocusFireTarget => HasLeader ? currentLeader.Blackboard.FocusFireTarget : null;
    public Transform LeaderTransform => HasLeader ? currentLeader.transform : null;
    public Vector3 RallyPoint => HasLeader ? currentLeader.Blackboard.RallyPoint : transform.position;

    public BaseAI BaseAI => baseAI ??= GetComponentInChildren<BaseAI>();

    private void Awake()
    {
        statsManager = GetComponent<CharacterStatsManager>() ?? GetComponentInChildren<CharacterStatsManager>();
        baseAI = GetComponentInChildren<BaseAI>();
    }

    private void Start()
    {
        if (currentLeader != null)
        {
            currentLeader.RegisterMember(this);
        }
    }

    private void OnDisable()
    {
        LeaveSquad();
    }

    public void AssignLeader(PartyLeader newLeader)
    {
        currentLeader = newLeader;
    }

    public void LeaveSquad()
    {
        if (currentLeader != null)
        {
            var leader = currentLeader;
            currentLeader = null;
            leader.UnregisterMember(this);
        }
    }
}
