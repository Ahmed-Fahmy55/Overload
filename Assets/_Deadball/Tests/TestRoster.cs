using System;
using System.Collections.Generic;
using Deadball.Fighters;
using UnityEngine;

namespace Deadball.Tests
{
    /// <summary>A roster the test fills directly, standing in for the join screen.</summary>
    public class TestRoster : MonoBehaviour, IFighterRoster
    {
        readonly List<Fighter> _fighters = new(2);

        public IReadOnlyList<Fighter> Fighters => _fighters;

        public bool IsReady => _fighters.Count >= 2;

        public event Action RosterComplete;

        public void Add(Fighter fighter)
        {
            _fighters.Add(fighter);

            if (IsReady) RosterComplete?.Invoke();
        }
    }
}
