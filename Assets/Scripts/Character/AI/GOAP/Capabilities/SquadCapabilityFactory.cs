using CrashKonijn.Goap.Core;
using CrashKonijn.Goap.Runtime;

public class SquadCapabilityFactory : CapabilityFactoryBase
{
    public override ICapabilityConfig Create()
    {
        var builder = new CapabilityBuilder("SquadCapability");

        // Goals
        builder.AddGoal<FollowLeaderGoal>()
            .AddCondition<IsIdle>(Comparison.GreaterThanOrEqual, 1)
            .SetBaseCost(1);

        builder.AddGoal<FocusFireGoal>()
            .AddCondition<ThreatLevel>(Comparison.SmallerThanOrEqual, 0)
            .SetBaseCost(1);

        builder.AddGoal<FallBackGoal>()
            .AddCondition<FearLevel>(Comparison.SmallerThanOrEqual, 0)
            .SetBaseCost(1);

        builder.AddGoal<StandbyGoal>()
            .AddCondition<IsIdle>(Comparison.GreaterThanOrEqual, 1)
            .SetBaseCost(1);

        builder.AddGoal<AlertAlliesGoal>()
            .AddCondition<HasAlertedAllies>(Comparison.GreaterThanOrEqual, 1)
            .SetBaseCost(1);

        builder.AddGoal<ScoutReconGoal>()
            .AddCondition<HasScoutedArea>(Comparison.GreaterThanOrEqual, 1)
            .SetBaseCost(1);

        builder.AddGoal<DebriefLeaderGoal>()
            .AddCondition<HasDebriefedLeader>(Comparison.GreaterThanOrEqual, 1)
            .SetBaseCost(1);

        // Actions
        builder.AddAction<FollowLeaderAction>()
            .SetTarget<LeaderTarget>()
            .AddEffect<IsIdle>(EffectType.Increase)
            .SetBaseCost(1);

        builder.AddAction<IdleAction>()
            .SetTarget<SelfTarget>()
            .AddEffect<IsIdle>(EffectType.Increase)
            .SetBaseCost(1);

        builder.AddAction<AlertAlliesAction>()
            .SetTarget<SelfTarget>()
            .AddEffect<HasAlertedAllies>(EffectType.Increase)
            .SetBaseCost(1);

        builder.AddAction<ScoutReconAction>()
            .SetTarget<ScoutReconTarget>()
            .AddEffect<HasScoutedArea>(EffectType.Increase)
            .SetBaseCost(1);

        builder.AddAction<DebriefLeaderAction>()
            .SetTarget<LeaderTarget>()
            .AddEffect<HasDebriefedLeader>(EffectType.Increase)
            .SetBaseCost(1);

        builder.AddAction<FocusFireAction>()
            .SetTarget<FocusFireTarget>()
            .AddEffect<ThreatLevel>(EffectType.Decrease)
            .SetBaseCost(1);

        builder.AddAction<FallBackAction>()
            .SetTarget<LeaderTarget>()
            .AddEffect<FearLevel>(EffectType.Decrease)
            .SetBaseCost(1);

        // Sensors
        builder.AddMultiSensor<SquadOrderSensor>();
        builder.AddTargetSensor<LeaderTargetSensor>().SetTarget<LeaderTarget>();
        builder.AddTargetSensor<FocusFireTargetSensor>().SetTarget<FocusFireTarget>();
        builder.AddTargetSensor<ScoutReconTargetSensor>().SetTarget<ScoutReconTarget>();

        return builder.Build();
    }
}
