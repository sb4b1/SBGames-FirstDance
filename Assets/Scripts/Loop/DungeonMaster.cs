using System;
using System.Collections.Generic;
using UnityEngine;

namespace SBGames.Loop
{
    /// <summary>
    /// Represents an artifact possessed by the Dungeon Master.
    /// Artifacts grant passive potency or abilities and act as anchors to the DM's existence.
    /// If all artifacts are lost or destroyed, the DM is defeated.
    /// </summary>
    [Serializable]
    public class Artifact
    {
        [SerializeField] private string id;
        [SerializeField] private string artifactName;
        [SerializeField] private int powerLevel;
        [SerializeField] private int essenceCost;

        public string Id => id;
        public string ArtifactName => artifactName;
        public int PowerLevel => powerLevel;
        public int EssenceCost => essenceCost;

        public Artifact(string artifactName, int powerLevel = 1, int essenceCost = 0, string id = null)
        {
            this.id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString() : id;
            this.artifactName = artifactName;
            this.powerLevel = Mathf.Max(1, powerLevel);
            this.essenceCost = Mathf.Max(0, essenceCost);
        }

        public void IncreasePower(int amount)
        {
            if (amount <= 0) return;
            powerLevel += amount;
        }
    }

    /// <summary>
    /// Reason for Dungeon Master defeat.
    /// </summary>
    public enum DefeatReason
    {
        None = 0,
        EssenceDepleted = 1,
        ArtifactsLost = 2
    }

    /// <summary>
    /// Persistent data container and resource management system for the Dungeon Master (player).
    /// Tracks vital essence (health), magical energy (currency), and artifact collection.
    /// </summary>
    [DisallowMultipleComponent]
    public class DungeonMaster : MonoBehaviour
    {
        [Header("Vitals (Health)")]
        [Tooltip("Maximum Essence the Dungeon Master can hold.")]
        [SerializeField] private int maxEssence = 100;

        [Tooltip("Current Essence. If this drops to 0, Game Over is triggered.")]
        [SerializeField] private int currentEssence = 100;

        [Header("Energy (Currency)")]
        [Tooltip("Maximum Energy capacity.")]
        [SerializeField] private int maxEnergy = 100;

        [Tooltip("Current Energy used to purchase monsters, empower artifacts, and recover essence.")]
        [SerializeField] private int currentEnergy = 50;

        [Header("Round Penalties & Defaults")]
        [Tooltip("Essence deducted when adventurers breach the dungeon defense during a round loss.")]
        [SerializeField] private int roundLossEssencePenalty = 25;

        [Tooltip("Default Energy cost required to recover Essence.")]
        [SerializeField] private int essenceRecoveryEnergyCost = 20;

        [Tooltip("Amount of Essence restored per recovery action.")]
        [SerializeField] private int essenceRecoveryAmount = 15;

        [Tooltip("Base Energy gained when sacrificing an artifact.")]
        [SerializeField] private int baseArtifactSacrificeEnergyReward = 25;

        [Header("Artifacts")]
        [Tooltip("Collection of artifacts bound to the Dungeon Master. If this becomes empty, Game Over is triggered.")]
        [SerializeField] private List<Artifact> artifacts = new List<Artifact>();

        // Events
        public event Action<int, int> OnEssenceChanged;
        public event Action<int, int> OnEnergyChanged;
        public event Action<Artifact> OnArtifactForged;
        public event Action<Artifact> OnArtifactEmpowered;
        public event Action<Artifact> OnArtifactRemoved;
        public event Action<Artifact, int> OnArtifactSacrificed;
        public event Action<DefeatReason> OnDefeated;

        // Properties
        public int Essence => currentEssence;
        public int MaxEssence => maxEssence;
        public int Energy => currentEnergy;
        public int MaxEnergy => maxEnergy;
        public int RoundLossEssencePenalty => roundLossEssencePenalty;
        public IReadOnlyList<Artifact> Artifacts => artifacts;
        public int ArtifactCount => artifacts.Count;
        public bool IsDefeated => currentEssence <= 0 || artifacts.Count == 0;

        private void Awake()
        {
            ClampValues();
            if (artifacts == null)
            {
                artifacts = new List<Artifact>();
            }
            if (artifacts.Count == 0)
            {
                artifacts.Add(new Artifact("Master's Heart", 1, 0));
            }
        }

        /// <summary>
        /// Programmatic initialization for runtime and unit test setups without scene dependency.
        /// </summary>
        public void Initialize(
            int startingEssence = 100,
            int startingEnergy = 50,
            IEnumerable<Artifact> startingArtifacts = null,
            int roundLossPenalty = 25,
            int maximumEssence = 100,
            int maximumEnergy = 100)
        {
            maxEssence = Mathf.Max(1, maximumEssence);
            maxEnergy = Mathf.Max(1, maximumEnergy);
            currentEssence = Mathf.Clamp(startingEssence, 0, maxEssence);
            currentEnergy = Mathf.Clamp(startingEnergy, 0, maxEnergy);
            roundLossEssencePenalty = Mathf.Max(0, roundLossPenalty);

            artifacts.Clear();
            if (startingArtifacts != null)
            {
                artifacts.AddRange(startingArtifacts);
            }
            else
            {
                // Default starting anchor artifact so player is not defeated on spawn
                artifacts.Add(new Artifact("Master's Heart", 1, 0));
            }

            OnEssenceChanged?.Invoke(currentEssence, maxEssence);
            OnEnergyChanged?.Invoke(currentEnergy, maxEnergy);
        }

        /// <summary>
        /// Attempts to spend the specified amount of energy.
        /// Returns true if successful, false if insufficient energy.
        /// </summary>
        public bool SpendEnergy(int amount)
        {
            if (amount <= 0) return true;
            if (currentEnergy < amount) return false;

            currentEnergy -= amount;
            OnEnergyChanged?.Invoke(currentEnergy, maxEnergy);
            return true;
        }

        /// <summary>
        /// Adds energy to the Dungeon Master, capped at MaxEnergy.
        /// </summary>
        public void GainEnergy(int amount)
        {
            if (amount <= 0) return;

            currentEnergy = Mathf.Min(currentEnergy + amount, maxEnergy);
            OnEnergyChanged?.Invoke(currentEnergy, maxEnergy);
        }

        /// <summary>
        /// Reduces the Dungeon Master's essence. If essence reaches 0, triggers defeat.
        /// </summary>
        public void LoseEssence(int amount)
        {
            if (amount <= 0) return;

            currentEssence = Mathf.Max(0, currentEssence - amount);
            OnEssenceChanged?.Invoke(currentEssence, maxEssence);

            CheckDefeatCondition();
        }

        /// <summary>
        /// Spends energy to recover essence up to MaxEssence.
        /// </summary>
        public bool RecoverEssence(int essenceAmount = -1, int energyCost = -1)
        {
            int cost = energyCost >= 0 ? energyCost : essenceRecoveryEnergyCost;
            int restoreAmount = essenceAmount >= 0 ? essenceAmount : essenceRecoveryAmount;

            if (currentEssence >= maxEssence) return false;
            if (!SpendEnergy(cost)) return false;

            currentEssence = Mathf.Min(currentEssence + restoreAmount, maxEssence);
            OnEssenceChanged?.Invoke(currentEssence, maxEssence);
            return true;
        }

        /// <summary>
        /// Convenience method to purchase monsters during the planning phase.
        /// </summary>
        public bool PurchaseMonster(int energyCost)
        {
            return SpendEnergy(energyCost);
        }

        /// <summary>
        /// Forges a new artifact by expending vital essence.
        /// Returns the newly forged Artifact, or null if insufficient essence.
        /// </summary>
        public Artifact ForgeArtifact(string artifactName, int essenceCost, int startingPower = 1)
        {
            if (string.IsNullOrEmpty(artifactName)) return null;
            if (essenceCost < 0) return null;

            // Cannot forge if cost exceeds current essence
            if (currentEssence < essenceCost) return null;

            LoseEssence(essenceCost);

            var artifact = new Artifact(artifactName, startingPower, essenceCost);
            artifacts.Add(artifact);
            OnArtifactForged?.Invoke(artifact);

            return artifact;
        }

        /// <summary>
        /// Empowers an existing artifact by spending energy to increase its power level.
        /// </summary>
        public bool EmpowerArtifact(Artifact artifact, int energyCost, int powerIncrease = 1)
        {
            if (artifact == null) return false;
            if (!artifacts.Contains(artifact)) return false;
            if (powerIncrease <= 0) return false;
            if (!SpendEnergy(energyCost)) return false;

            artifact.IncreasePower(powerIncrease);
            OnArtifactEmpowered?.Invoke(artifact);
            return true;
        }

        /// <summary>
        /// Empowers an existing artifact looked up by its unique ID.
        /// </summary>
        public bool EmpowerArtifact(string artifactId, int energyCost, int powerIncrease = 1)
        {
            var artifact = artifacts.Find(a => a.Id == artifactId);
            return EmpowerArtifact(artifact, energyCost, powerIncrease);
        }

        /// <summary>
        /// Sacrifices an artifact in exchange for immediate emergency Energy.
        /// If this empties the artifact collection, triggers defeat.
        /// </summary>
        public bool SacrificeArtifact(Artifact artifact, int energyReward = -1)
        {
            if (artifact == null) return false;
            if (!artifacts.Contains(artifact)) return false;

            int energyToGain = energyReward >= 0 ? energyReward : baseArtifactSacrificeEnergyReward;

            artifacts.Remove(artifact);
            GainEnergy(energyToGain);

            OnArtifactSacrificed?.Invoke(artifact, energyToGain);
            OnArtifactRemoved?.Invoke(artifact);

            CheckDefeatCondition();
            return true;
        }

        /// <summary>
        /// Removes an artifact from the Dungeon Master's collection (e.g. dropped when an equipped monster dies or looted).
        /// If this causes the artifact collection to become empty, triggers defeat.
        /// </summary>
        public bool RemoveArtifact(Artifact artifact)
        {
            if (artifact == null) return false;

            bool removed = artifacts.Remove(artifact);
            if (removed)
            {
                OnArtifactRemoved?.Invoke(artifact);
                CheckDefeatCondition();
            }

            return removed;
        }

        /// <summary>
        /// Clears all artifacts. Triggers defeat due to empty artifact collection.
        /// </summary>
        public void ClearArtifacts()
        {
            artifacts.Clear();
            CheckDefeatCondition();
        }

        /// <summary>
        /// Applies the essence penalty when a round defense is lost.
        /// </summary>
        public void ApplyRoundLossPenalty(int? customPenalty = null)
        {
            int penalty = customPenalty.HasValue ? Mathf.Max(0, customPenalty.Value) : roundLossEssencePenalty;
            LoseEssence(penalty);
        }

        /// <summary>
        /// Evaluates whether the Dungeon Master is defeated and broadcasts the reason.
        /// </summary>
        public DefeatReason CheckDefeatCondition()
        {
            if (currentEssence <= 0)
            {
                OnDefeated?.Invoke(DefeatReason.EssenceDepleted);
                return DefeatReason.EssenceDepleted;
            }

            if (artifacts.Count == 0)
            {
                OnDefeated?.Invoke(DefeatReason.ArtifactsLost);
                return DefeatReason.ArtifactsLost;
            }

            return DefeatReason.None;
        }

        private void ClampValues()
        {
            maxEssence = Mathf.Max(1, maxEssence);
            maxEnergy = Mathf.Max(1, maxEnergy);
            currentEssence = Mathf.Clamp(currentEssence, 0, maxEssence);
            currentEnergy = Mathf.Clamp(currentEnergy, 0, maxEnergy);
            roundLossEssencePenalty = Mathf.Max(0, roundLossEssencePenalty);
        }
    }
}
