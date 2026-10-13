using System;
using UnityEngine;

namespace SBGames.Characters.AI
{
    [CreateAssetMenu(fileName = "RoomLoopTuning", menuName = "SBGames/AI/Room Loop Tuning", order = 2)]
    public class RoomLoopTuningSO : ScriptableObject
    {
        [Header("Fork Selection Weights")]
        [Tooltip("Weight given to distance when selecting an unexplored exit (lower distance is preferred).")]
        [SerializeField, Range(0.1f, 5.0f)] private float nearestExitWeight = 1.0f;

        [Tooltip("Weight given to avoiding known hazards or hazardous rooms.")]
        [SerializeField, Range(0f, 10.0f)] private float hazardAvoidanceWeight = 0f;

        [Tooltip("Weight given to information gathered during scout peeks.")]
        [SerializeField, Range(0f, 5.0f)] private float scoutInfoWeight = 0f;

        [Header("Timeouts & Durations (seconds)")]
        [Tooltip("Maximum duration the party leader waits for straggling members during Staging before proceeding.")]
        [SerializeField, Range(1.0f, 15.0f)] private float stragglerTimeout = 5.0f;

        [Tooltip("Maximum duration to regroup after combat before forcing a checkpoint decision.")]
        [SerializeField, Range(1.0f, 10.0f)] private float regroupTimeout = 4.0f;

        [Tooltip("Duration the party spends recovering inside a safe room before moving on.")]
        [SerializeField, Range(2.0f, 30.0f)] private float safeRoomDuration = 10.0f;

        [Tooltip("Maximum duration spent waiting at the door before entering or peeking.")]
        [SerializeField, Range(0.1f, 5.0f)] private float stagingHesitationTimeout = 1.5f;

        [Header("Boss Door Behavior")]
        [Tooltip("If true, party calls RequestCheckpoint when reaching the recognized boss door.")]
        [SerializeField] private bool preBossCheckpoint = false;

        [Tooltip("If true, party takes a Recover order to use consumables before passing the boss door.")]
        [SerializeField] private bool preBossRecover = false;

        public float NearestExitWeight => nearestExitWeight;
        public float HazardAvoidanceWeight => hazardAvoidanceWeight;
        public float ScoutInfoWeight => scoutInfoWeight;
        public float StragglerTimeout => stragglerTimeout;
        public float RegroupTimeout => regroupTimeout;
        public float SafeRoomDuration => safeRoomDuration;
        public float StagingHesitationTimeout => stagingHesitationTimeout;
        public bool PreBossCheckpoint => preBossCheckpoint;
        public bool PreBossRecover => preBossRecover;

        public static RoomLoopTuningSO CreateDefault(CharacterRank rank)
        {
            var tuning = CreateInstance<RoomLoopTuningSO>();
            switch (rank)
            {
                case CharacterRank.E:
                    tuning.nearestExitWeight = 1.0f;
                    tuning.hazardAvoidanceWeight = 0.0f;
                    tuning.scoutInfoWeight = 0.0f;
                    tuning.stragglerTimeout = 4.0f;
                    tuning.regroupTimeout = 3.0f;
                    tuning.safeRoomDuration = 8.0f;
                    tuning.stagingHesitationTimeout = 1.0f;
                    tuning.preBossCheckpoint = false;
                    tuning.preBossRecover = false;
                    break;
                case CharacterRank.D:
                    tuning.nearestExitWeight = 1.0f;
                    tuning.hazardAvoidanceWeight = 1.0f;
                    tuning.scoutInfoWeight = 1.0f;
                    tuning.stragglerTimeout = 5.0f;
                    tuning.regroupTimeout = 4.0f;
                    tuning.safeRoomDuration = 10.0f;
                    tuning.stagingHesitationTimeout = 1.2f;
                    tuning.preBossCheckpoint = true;
                    tuning.preBossRecover = false;
                    break;
                case CharacterRank.C:
                default:
                    tuning.nearestExitWeight = 1.0f;
                    tuning.hazardAvoidanceWeight = 3.0f;
                    tuning.scoutInfoWeight = 2.5f;
                    tuning.stragglerTimeout = 6.0f;
                    tuning.regroupTimeout = 5.0f;
                    tuning.safeRoomDuration = 12.0f;
                    tuning.stagingHesitationTimeout = 1.5f;
                    tuning.preBossCheckpoint = true;
                    tuning.preBossRecover = true;
                    break;
            }
            return tuning;
        }
    }
}
