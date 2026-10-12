using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SquadMember : MonoBehaviour, ISquadMember
{
    [SerializeField] private PartyLeader currentLeader;
    [SerializeField] private bool overrideIsScout = false;

    private CharacterStatsManager statsManager;
    private BaseAI baseAI;
    private bool isScouting = false;
    private bool isStayingLoose = false;
    private readonly List<SquadMember> loosePeers = new(8);

    public GameObject GameObject => gameObject;
    public PartyLeader Leader => currentLeader;
    public bool HasLeader => currentLeader != null && currentLeader.IsAlive;
    public bool IsStayingLoose => isStayingLoose && !HasLeader;
    public bool IsAlive => statsManager == null || statsManager.Life > 0;

    public AdventurerFunction CharacterFunction => StatsManager != null ? StatsManager.CharacterFunction : AdventurerFunction.None;
    public AdventurerFunction EffectiveFunction => StatsManager != null ? StatsManager.EffectiveFunction : AdventurerFunction.DPS;
    public AdventurerWealth CharacterWealth => StatsManager != null ? StatsManager.CharacterWealth : AdventurerWealth.Average;

    public bool IsScout
    {
        get
        {
            if (overrideIsScout) return true;
            if (StatsManager == null) return false;
            return StatsManager.CharacterFunction == AdventurerFunction.Scout
                || StatsManager.CharacterClass == CharacterClass.Scout
                || StatsManager.CharacterClass == CharacterClass.Rogue;
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
            if (!HasLeader) return SquadOrderType.None;

            SquadOrderType order = currentLeader.Blackboard.CurrentOrder;
            if (order == SquadOrderType.ScoutAhead && CharacterFunction != AdventurerFunction.Scout)
            {
                return SquadOrderType.FollowLeader;
            }
            return order;
        }
    }

    public Transform FocusFireTarget => HasLeader ? currentLeader.Blackboard.FocusFireTarget : null;
    public Transform LeaderTransform
    {
        get
        {
            if (HasLeader) return currentLeader.transform;
            if (isStayingLoose)
            {
                var nearestPeer = GetNearestLoosePeer();
                return nearestPeer != null ? nearestPeer.transform : null;
            }
            return null;
        }
    }

    public Vector3 RallyPoint => HasLeader ? currentLeader.Blackboard.RallyPoint : transform.position;

    public Vector3 GetFormationTargetPosition()
    {
        if (HasLeader)
        {
            return currentLeader.GetFormationWorldPosition(this);
        }

        if (isStayingLoose)
        {
            var nearestPeer = GetNearestLoosePeer();
            if (nearestPeer != null)
            {
                return nearestPeer.transform.position;
            }
        }

        return transform.position;
    }

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
        if (newLeader != null)
        {
            isStayingLoose = false;
            loosePeers.Clear();
        }
        if (currentLeader == newLeader) return;
        currentLeader = newLeader;
    }

    public void LeaveSquad()
    {
        isScouting = false;
        isStayingLoose = false;
        loosePeers.Clear();
        if (currentLeader != null)
        {
            var leader = currentLeader;
            currentLeader = null;
            leader.UnregisterMember(this);
        }
    }

    public void EnterStayLooseMode(IReadOnlyList<SquadMember> peers)
    {
        currentLeader = null;
        isScouting = false;
        isStayingLoose = true;
        loosePeers.Clear();

        if (peers == null) return;
        for (int i = 0; i < peers.Count; i++)
        {
            var peer = peers[i];
            if (peer != null && peer != this && peer.IsAlive)
            {
                loosePeers.Add(peer);
            }
        }
    }

    public void DissolveFromSquad()
    {
        currentLeader = null;
        isScouting = false;
        isStayingLoose = false;
        loosePeers.Clear();
    }

    public SquadMember GetNearestLoosePeer()
    {
        if (!isStayingLoose || loosePeers.Count == 0) return null;

        SquadMember nearest = null;
        float bestSqrDist = float.MaxValue;
        Vector3 myPos = transform.position;

        for (int i = 0; i < loosePeers.Count; i++)
        {
            var peer = loosePeers[i];
            if (peer == null || !peer.IsAlive || peer == this) continue;

            float sqrDist = (peer.transform.position - myPos).sqrMagnitude;
            if (sqrDist < bestSqrDist)
            {
                bestSqrDist = sqrDist;
                nearest = peer;
            }
        }

        return nearest;
    }
}
