using System;
using System.Collections;
using System.Collections.Generic;
using Deadball.Ball;
using Deadball.Fighters;
using Deadball.Match;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deadball.AI
{
    /// <summary>
    /// Solo mode's roster: one human, one house runner (OVERLOAD GDD section 11).
    /// </summary>
    /// <remarks>
    /// The match director asks an <see cref="IFighterRoster"/> for its runners and never learns what
    /// is driving them, so Solo is this class and Local Versus is the join screen - the design's
    /// claim that the only difference between the modes is what drives player two is literally true
    /// rather than aspirational.
    /// </remarks>
    public class SoloRoster : MonoBehaviour, IFighterRoster
    {
        [Title("Spawning")]
        [Required, SerializeField] GameObject _runnerPrefab;
        [Required, SerializeField] ArenaReferences _arena;
        [Required, SerializeField] BallController _core;

        [Tooltip("Optional. Lets the house runner read the core's heat (13.5).")]
        [SerializeField] RallyHeat _heat;

        [Title("Difficulty")]
        [Required, SerializeField] AiProfile _profile;

        [Title("Runtime")]
        [ShowInInspector, ReadOnly]
        public IReadOnlyList<Fighter> Fighters => _fighters;

        [ShowInInspector, ReadOnly]
        public bool IsReady => _fighters.Count >= 2;

        public event Action RosterComplete;

        readonly List<Fighter> _fighters = new(2);

        /// <summary>Swaps the tier before a match. One float, three opponents (13.3).</summary>
        public AiProfile Profile
        {
            get => _profile;
            set => _profile = value;
        }

        IEnumerator Start()
        {
            // A frame so the arena and core have finished waking before anything is spawned.
            yield return null;
            Spawn();
        }

        [Button("Spawn Runners"), DisableInEditorMode]
        public void Spawn()
        {
            if (IsReady) return;

            SpawnHuman(0);
            SpawnHouseRunner(1);

            if (IsReady) RosterComplete?.Invoke();
        }

        void SpawnHuman(int slot)
        {
            GameObject instance = Instantiate(_runnerPrefab);
            instance.name = "Runner_Player";

            var fighter = instance.GetComponent<Fighter>();
            var provider = instance.GetComponent<Deadball.Input.PlayerInputProvider>();

            fighter.Bind(slot, provider);
            Place(fighter, slot);
            _fighters.Add(fighter);
        }

        void SpawnHouseRunner(int slot)
        {
            GameObject instance = Instantiate(_runnerPrefab);
            instance.name = "Runner_House";

            // The house runner has no device, so its Input System components come off entirely -
            // what remains is a runner that reads the same four inputs from a state machine.
            if (instance.GetComponent<Deadball.Input.PlayerInputProvider>() is { } provider)
                DestroyImmediate(provider);
            if (instance.GetComponent<PlayerInput>() is { } playerInput)
                DestroyImmediate(playerInput);

            var fighter = instance.GetComponent<Fighter>();
            var brain = instance.AddComponent<AiInputSource>();

            brain.Configure(_profile, fighter, _core, _heat);

            fighter.Bind(slot, brain);
            Place(fighter, slot);
            _fighters.Add(fighter);
        }

        void Place(Fighter fighter, int slot)
        {
            _arena.GetSpawn(slot, handicapped: false, out Vector3 position, out Quaternion rotation);
            fighter.PrepareForRound(position, rotation);
        }
    }
}
