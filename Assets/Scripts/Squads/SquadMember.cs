using UnityEngine;

[DisallowMultipleComponent]
public class SquadMember : MonoBehaviour, ISquadMember
{
    [SerializeField] private PartyLeader currentLeader;
    [SerializeField] private bool overrideIsScout = false;

    private CharacterStatsManager statsManager;
    private BaseAI baseAI;
    private bool isScouting = false;

    public GameObject GameObject => gameObject;
    public PartyLeader Leader => currentLeader;
    public bool HasLeader => currentLeader != null && currentLeader.IsAlive;
    public bool IsAlive => statsManager == null || statsManager.Life > 0;

    public bool IsScout
    {
        get
        {
            if (overrideIsScout) return true;
            return StatsManager != null && (StatsManager.CharacterClass == CharacterClass.Scout || StatsManager.CharacterClass == CharacterClass.Rogue);
        }
        set => overrideIsScout = value;
    }

    public bool IsScouting
    {
        get => isScouting;
        set => isScouting = value;
    }

    public SquadOrderType CurrentOrder
    {
        get
        {
            if (isScouting) return SquadOrderType.ScoutAhead;
            return HasLeader ? currentLeader.Blackboard.CurrentOrder : SquadOrderType.None;
        }
    }
    public Transform FocusFireTarget => HasLeader ? currentLeader.Blackboard.FocusFireTarget : null;
    public Transform LeaderTransform => HasLeader ? currentLeader.transform : null;
    public Vector3 RallyPoint => HasLeader ? currentLeader.Blackboard.RallyPoint : transform.position;

    public BaseAI BaseAI => baseAI ??= GetComponentInChildren<BaseAI>();
    public CharacterStatsManager StatsManager => statsManager ??= (GetComponent<CharacterStatsManager>() ?? GetComponentInChildren<CharacterStatsManager>());

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
        if (currentLeader == newLeader) return;
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
